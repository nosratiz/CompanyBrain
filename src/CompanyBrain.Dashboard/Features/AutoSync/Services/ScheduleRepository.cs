using CompanyBrain.Dashboard.Data;
using CompanyBrain.Dashboard.Features.AutoSync.Models;
using Microsoft.EntityFrameworkCore;

namespace CompanyBrain.Dashboard.Features.AutoSync.Services;

/// <summary>
/// Data-access layer for <see cref="SyncSchedule"/> records.
/// All methods open a fresh <see cref="DocumentAssignmentDbContext"/> per call,
/// keeping the repository safe to use from the singleton <see cref="SovereignSyncWorker"/>.
/// </summary>
public sealed class ScheduleRepository(
    IDbContextFactory<DocumentAssignmentDbContext> dbContextFactory,
    ILogger<ScheduleRepository> logger) : IScheduleRepository
{
    // ── Queries ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Returns all active <see cref="SyncSchedule"/> records, including those currently
    /// in back-off (callers must evaluate <see cref="SyncSchedule.NextRetryUtc"/> themselves).
    /// </summary>
    public async Task<IReadOnlyList<SyncSchedule>> GetActiveSchedulesAsync(CancellationToken cancellationToken)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        return await db.SyncSchedules
            .Where(s => s.IsActive)
            .AsNoTracking()
            .ToListAsync(cancellationToken);
    }

    // ── State mutations ───────────────────────────────────────────────────────

    /// <summary>
    /// Marks a schedule as having completed successfully.
    /// Updates <see cref="SyncSchedule.LastSyncUtc"/>, stores the new content hash,
    /// and clears all failure state.
    /// </summary>
    public async Task UpdateAfterSuccessAsync(
        int scheduleId,
        string? contentHash,
        CancellationToken cancellationToken)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var schedule = await db.SyncSchedules.FindAsync([scheduleId], cancellationToken);

        if (schedule is null)
        {
            logger.LogWarning("UpdateAfterSuccessAsync: schedule {Id} not found", scheduleId);
            return;
        }

        schedule.LastSyncUtc = DateTime.UtcNow;
        schedule.LastContentHash = contentHash;
        schedule.LastErrorMessage = null;
        schedule.ConsecutiveFailureCount = 0;
        schedule.NextRetryUtc = null;

        await db.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Records a sync failure, increments the failure counter, sets the human-readable
    /// error, and calculates the next retry time using exponential back-off.
    /// </summary>
    public async Task UpdateAfterFailureAsync(
        int scheduleId,
        string errorMessage,
        CancellationToken cancellationToken)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var schedule = await db.SyncSchedules.FindAsync([scheduleId], cancellationToken);

        if (schedule is null)
        {
            logger.LogWarning("UpdateAfterFailureAsync: schedule {Id} not found", scheduleId);
            return;
        }

        schedule.ConsecutiveFailureCount++;
        schedule.LastErrorMessage = errorMessage.Length > 2000
            ? errorMessage[..2000]
            : errorMessage;
        schedule.NextRetryUtc = ComputeNextRetryUtc(schedule.ConsecutiveFailureCount);

        logger.LogDebug(
            "Schedule {Id} back-off #{Count} — next retry at {NextRetry:u}",
            scheduleId, schedule.ConsecutiveFailureCount, schedule.NextRetryUtc);

        await db.SaveChangesAsync(cancellationToken);
    }

    // ── CRUD helpers (used by management API / UI) ────────────────────────────

    /// <summary>Persists a new schedule record and returns the saved entity.</summary>
    public async Task<SyncSchedule> CreateAsync(SyncSchedule schedule, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(schedule);

        schedule.CreatedAtUtc = DateTime.UtcNow;

        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        db.SyncSchedules.Add(schedule);
        await db.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Created sync schedule {Id} for {Url} ({SourceType}, cron: {Cron})",
            schedule.Id, schedule.SourceUrl, schedule.SourceType, schedule.CronExpression);

        return schedule;
    }

    /// <summary>Toggles <see cref="SyncSchedule.IsActive"/> for the given ID.</summary>
    public async Task<bool> SetActiveAsync(int scheduleId, bool isActive, CancellationToken cancellationToken)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var schedule = await db.SyncSchedules.FindAsync([scheduleId], cancellationToken);

        if (schedule is null)
            return false;

        schedule.IsActive = isActive;
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }

    /// <summary>Deletes the schedule with the given ID. Returns false when not found.</summary>
    public async Task<bool> DeleteAsync(int scheduleId, CancellationToken cancellationToken)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var schedule = await db.SyncSchedules.FindAsync([scheduleId], cancellationToken);

        if (schedule is null)
            return false;

        db.SyncSchedules.Remove(schedule);
        await db.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Deleted sync schedule {Id}", scheduleId);
        return true;
    }

    // ── Back-off calculation ──────────────────────────────────────────────────

    /// <summary>
    /// Exponential back-off: 5 min → 15 min → 1 h → 4 h → 24 h (capped).
    /// </summary>
    internal static DateTime ComputeNextRetryUtc(int consecutiveFailureCount) =>
        consecutiveFailureCount switch
        {
            1 => DateTime.UtcNow.AddMinutes(5),
            2 => DateTime.UtcNow.AddMinutes(15),
            3 => DateTime.UtcNow.AddHours(1),
            4 => DateTime.UtcNow.AddHours(4),
            _ => DateTime.UtcNow.AddHours(24),
        };
}
