using System.Collections.Concurrent;
using Cronos;
using CompanyBrain.Dashboard.Data.Audit;
using CompanyBrain.Dashboard.Features.AutoSync.Models;
using CompanyBrain.Dashboard.Features.AutoSync.Providers;
using CompanyBrain.Dashboard.Services.Audit;

namespace CompanyBrain.Dashboard.Features.AutoSync.Services;

/// <summary>
/// Background worker that evaluates all active <see cref="SyncSchedule"/> records every
/// two minutes, fires the appropriate <see cref="IIngestionProvider"/> for each due entry,
/// and updates the schedule state in SQLite.
///
/// <para><b>Thread-safety:</b> a <see cref="SemaphoreSlim(1,1)"/> per schedule ID prevents
/// a slow sync from re-entering itself if the next tick fires before the previous one finishes.</para>
///
/// <para><b>Back-off:</b> failing schedules are suppressed for 5 min / 15 min / 1 h / 4 h / 24 h
/// on consecutive failures.  Errors are stored in <see cref="SyncSchedule.LastErrorMessage"/>
/// so operators can diagnose problems from the UI without crashing the worker loop.</para>
///
/// <para><b>Delta detection:</b> providers that handle their own delta (SharePoint Graph,
/// Confluence version numbers) return <c>ContentHash = null</c>.  HTML-based providers
/// (WebWiki, GitHub) return a SHA-256 hex hash; the worker stores it in
/// <see cref="SyncSchedule.LastContentHash"/> so future runs can skip unchanged pages.</para>
/// </summary>
public sealed class SovereignSyncWorker(
    IScheduleRepository scheduleRepository,
    IngestionProviderFactory providerFactory,
    IServiceScopeFactory scopeFactory,
    ILogger<SovereignSyncWorker> logger) : BackgroundService
{
    /// <summary>How often the worker wakes up to check for due schedules.</summary>
    public static readonly TimeSpan CheckInterval = TimeSpan.FromMinutes(2);

    /// <summary>
    /// One semaphore slot per schedule ID — prevents the same URL from running
    /// in two concurrent iterations simultaneously.
    /// </summary>
    private readonly ConcurrentDictionary<int, SemaphoreSlim> _runningLocks = new();

    // ── BackgroundService lifecycle ───────────────────────────────────────────

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation(
            "SovereignSyncWorker started (check interval: {Interval})", CheckInterval);

        // Let the rest of the application initialize before the first check
        await Task.Delay(TimeSpan.FromSeconds(45), stoppingToken);

        using var timer = new PeriodicTimer(CheckInterval);

        while (!stoppingToken.IsCancellationRequested
               && await timer.WaitForNextTickAsync(stoppingToken))
        {
            await RunCheckCycleAsync(stoppingToken);
        }

        logger.LogInformation("SovereignSyncWorker stopped");
    }

    // ── Public API (for manual / test triggers) ────────────────────────────────

    /// <summary>
    /// Triggers an immediate sync cycle outside the normal timer tick.
    /// Useful for "run now" buttons in the UI and integration tests.
    /// </summary>
    public Task TriggerImmediateAsync(CancellationToken cancellationToken = default)
        => RunCheckCycleAsync(cancellationToken);

    /// <summary>
    /// Forces all active schedules to run immediately, bypassing the cron due-check.
    /// Intended for the "Sync Now" button so the user always gets a fresh sync regardless
    /// of when the last scheduled run occurred.
    /// </summary>
    public async Task ForceRunAllAsync(CancellationToken cancellationToken = default)
    {
        IReadOnlyList<SyncSchedule> schedules;
        try
        {
            schedules = await scheduleRepository.GetActiveSchedulesAsync(cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "SovereignSyncWorker: ForceRunAll failed to fetch active schedules");
            return;
        }

        if (schedules.Count == 0)
        {
            logger.LogDebug("SovereignSyncWorker: ForceRunAll — no active schedules");
            return;
        }

        logger.LogInformation("SovereignSyncWorker: ForceRunAll — running {Count} schedule(s)", schedules.Count);
        await Task.WhenAll(schedules.Select(s => ProcessScheduleAsync(s, cancellationToken)));
    }

    // ── Core loop ─────────────────────────────────────────────────────────────

    private async Task RunCheckCycleAsync(CancellationToken ct)
    {
        IReadOnlyList<SyncSchedule> schedules;
        try
        {
            schedules = await scheduleRepository.GetActiveSchedulesAsync(ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            return;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "SovereignSyncWorker: failed to fetch active schedules");
            return;
        }

        var due = schedules.Where(IsDue).ToList();

        if (due.Count == 0)
        {
            logger.LogDebug("SovereignSyncWorker: no due schedules this cycle");
            return;
        }

        logger.LogInformation("SovereignSyncWorker: {Count} schedule(s) due this cycle", due.Count);

        // Process all due schedules concurrently; each is guarded by its own semaphore
        await Task.WhenAll(due.Select(s => ProcessScheduleAsync(s, ct)));
    }

    // ── Schedule evaluation ───────────────────────────────────────────────────

    private static bool IsDue(SyncSchedule schedule)
    {
        // Back-off gate: skip until the retry window has passed
        if (schedule.NextRetryUtc.HasValue && schedule.NextRetryUtc.Value > DateTime.UtcNow)
            return false;

        try
        {
            return CronEvaluator.IsDue(schedule.CronExpression, schedule.LastSyncUtc);
        }
        catch (CronFormatException ex)
        {
            // Invalid expression stored in the DB — log once and skip silently
            // (the UI/API layer validates cron at creation time, so this is rare)
            _ = ex;
            return false;
        }
    }

    // ── Per-schedule execution ────────────────────────────────────────────────

    private async Task ProcessScheduleAsync(SyncSchedule schedule, CancellationToken ct)
    {
        // Acquire per-ID lock (non-blocking — skip if the previous run is still in flight)
        var sem = _runningLocks.GetOrAdd(schedule.Id, _ => new SemaphoreSlim(1, 1));

        if (!await sem.WaitAsync(TimeSpan.Zero, ct))
        {
            logger.LogDebug(
                "SovereignSyncWorker: schedule {Id} ({Url}) still running — skipping this tick",
                schedule.Id, schedule.SourceUrl);
            return;
        }

        try
        {
            await RunProviderAsync(schedule, ct);
        }
        finally
        {
            sem.Release();
        }
    }

    private async Task RunProviderAsync(SyncSchedule schedule, CancellationToken ct)
    {
        logger.LogInformation(
            "SovereignSyncWorker: starting sync for schedule {Id}: {Url} [{SourceType}]",
            schedule.Id, schedule.SourceUrl, schedule.SourceType);

        var provider = providerFactory.GetProvider(schedule.SourceType);

        if (provider is null)
        {
            var msg = $"No IIngestionProvider registered for SourceType '{schedule.SourceType}'.";
            logger.LogWarning("SovereignSyncWorker: {Message}", msg);
            await SafeUpdateFailureAsync(schedule.Id, msg, ct);
            return;
        }

        IngestionResult result;
        try
        {
            result = await provider.SyncAsync(schedule, ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            logger.LogInformation("SovereignSyncWorker: schedule {Id} cancelled during shutdown", schedule.Id);
            return;
        }
        catch (Exception ex)
        {
            logger.LogError(ex,
                "SovereignSyncWorker: unhandled exception in provider {Provider} for schedule {Id}",
                provider.GetType().Name, schedule.Id);
            await SafeUpdateFailureAsync(schedule.Id, ex.Message, ct);
            return;
        }

        if (result.Success)
        {
            await scheduleRepository.UpdateAfterSuccessAsync(schedule.Id, result.ContentHash, ct);

            if (result.ContentChanged)
                logger.LogInformation(
                    "SovereignSyncWorker: schedule {Id} synced — content CHANGED, knowledge store updated",
                    schedule.Id);
            else
                logger.LogDebug(
                    "SovereignSyncWorker: schedule {Id} synced — content unchanged, embedding skipped",
                    schedule.Id);
        }
        else
        {
            logger.LogWarning(
                "SovereignSyncWorker: schedule {Id} sync FAILED: {Error}",
                schedule.Id, result.ErrorMessage);
            await SafeUpdateFailureAsync(schedule.Id, result.ErrorMessage ?? "Unknown error", ct);
        }

        await WriteAuditAsync(schedule, result, ct);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private async Task WriteAuditAsync(SyncSchedule schedule, IngestionResult result, CancellationToken ct)
    {
        try
        {
            using var scope = scopeFactory.CreateScope();
            var audit = scope.ServiceProvider.GetRequiredService<IAuditService>();
            await audit.LogAsync(AuditEventType.SyncScheduleRun, new AuditEntry(
                ActorId: "system",
                ResourceType: "SyncSchedule",
                ResourceId: schedule.Id.ToString(),
                ResourceName: schedule.SourceUrl,
                Metadata: new { sourceType = schedule.SourceType.ToString(), contentChanged = result.ContentChanged },
                Success: result.Success,
                ErrorMessage: result.ErrorMessage));
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "SovereignSyncWorker: audit write failed for schedule {Id}", schedule.Id);
        }
    }

    private async Task SafeUpdateFailureAsync(int scheduleId, string error, CancellationToken ct)
    {
        try
        {
            await scheduleRepository.UpdateAfterFailureAsync(scheduleId, error, ct);
        }
        catch (Exception ex)
        {
            // A DB write failure should never crash the worker loop
            logger.LogError(ex,
                "SovereignSyncWorker: failed to persist failure state for schedule {Id}", scheduleId);
        }
    }
}
