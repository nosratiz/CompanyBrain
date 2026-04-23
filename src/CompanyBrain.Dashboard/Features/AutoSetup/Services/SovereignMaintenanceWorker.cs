using System.Text.RegularExpressions;
using CompanyBrain.Dashboard.Features.SharePoint.Data;
using Microsoft.EntityFrameworkCore;

namespace CompanyBrain.Dashboard.Features.AutoSetup.Services;

/// <summary>
/// Background worker that performs automatic housekeeping:
/// - Prunes indexing fragments older than a configurable threshold (default 48h)
/// - Scrubs accidentally indexed PII from the local SQLite index
/// Runs on a 6-hour cycle by default.
/// </summary>
public sealed class SovereignMaintenanceWorker(
    IServiceScopeFactory scopeFactory,
    ILogger<SovereignMaintenanceWorker> logger) : BackgroundService
{
    private static readonly TimeSpan InitialDelay = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan CycleInterval = TimeSpan.FromHours(6);
    private static readonly TimeSpan DefaultPruneAge = TimeSpan.FromHours(48);

    /// <summary>
    /// PII patterns for scrubbing extracted content.
    /// Matches common formats — intentionally conservative to avoid false positives.
    /// </summary>
    private static readonly Regex[] PiiPatterns =
    [
        // Swedish personal identity number (personnummer): YYYYMMDD-XXXX or YYMMDD-XXXX
        new(@"\b\d{6,8}[-\s]?\d{4}\b", RegexOptions.Compiled, TimeSpan.FromSeconds(5)),

        // Credit card numbers (13-19 digits with optional separators)
        new(@"\b(?:\d[ -]*?){13,19}\b", RegexOptions.Compiled, TimeSpan.FromSeconds(5)),

        // Email addresses
        new(@"\b[A-Za-z0-9._%+\-]+@[A-Za-z0-9.\-]+\.[A-Za-z]{2,}\b", RegexOptions.Compiled, TimeSpan.FromSeconds(5)),

        // Phone numbers (international format)
        new(@"\+?\d{1,4}[\s\-]?\(?\d{1,4}\)?[\s\-]?\d{3,4}[\s\-]?\d{3,4}\b", RegexOptions.Compiled, TimeSpan.FromSeconds(5)),

        // US Social Security Numbers
        new(@"\b\d{3}[-\s]?\d{2}[-\s]?\d{4}\b", RegexOptions.Compiled, TimeSpan.FromSeconds(5))
    ];

    private const string PiiRedaction = "[REDACTED]";

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Sovereign Maintenance Worker started");

        // Wait before first cycle to let the app fully initialize
        try
        {
            await Task.Delay(InitialDelay, stoppingToken);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                logger.LogInformation("Maintenance cycle starting");

                await RunPruneAsync(stoppingToken);
                await RunPiiScrubAsync(stoppingToken);

                logger.LogInformation("Maintenance cycle complete");
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Maintenance cycle failed");
            }

            try
            {
                await Task.Delay(CycleInterval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }

        logger.LogInformation("Sovereign Maintenance Worker stopped");
    }

    /// <summary>
    /// Auto-prune: deletes synced file records and their local files when they are
    /// older than <see cref="DefaultPruneAge"/> and the file no longer exists on
    /// disk (i.e., orphaned index entries from deleted remote files).
    /// </summary>
    private async Task RunPruneAsync(CancellationToken cancellationToken)
    {
        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var dbFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<SharePointDbContext>>();
            await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);

            var cutoff = DateTime.UtcNow - DefaultPruneAge;

            // Find orphaned files: last synced > 48h ago AND local file is missing
            var candidates = await db.SyncedFiles
                .Where(f => f.LastSyncedAtUtc < cutoff)
                .ToListAsync(cancellationToken);

            var pruned = 0;
            foreach (var file in candidates)
            {
                if (File.Exists(file.LocalPath))
                    continue; // File still exists on disk — keep the index entry

                db.SyncedFiles.Remove(file);
                pruned++;
            }

            if (pruned > 0)
            {
                await db.SaveChangesAsync(cancellationToken);
                logger.LogInformation("Pruned {Count} orphaned index entries older than {Hours}h",
                    pruned, DefaultPruneAge.TotalHours);
            }
            else
            {
                logger.LogDebug("No orphaned entries to prune");
            }

            // Also clean up resolved conflicts older than the threshold
            var conflictsPruned = await db.SyncConflicts
                .Where(c => c.Status != "Pending" && c.ResolvedAtUtc < cutoff)
                .ExecuteDeleteAsync(cancellationToken);

            if (conflictsPruned > 0)
                logger.LogInformation("Pruned {Count} resolved conflict records", conflictsPruned);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Prune phase failed");
        }
    }

    /// <summary>
    /// Privacy scrub: scans ExtractedContent in the synced files index for PII patterns
    /// and redacts matches in-place.
    /// </summary>
    private async Task RunPiiScrubAsync(CancellationToken cancellationToken)
    {
        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var dbFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<SharePointDbContext>>();
            await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);

            // Process in batches to avoid loading the entire table into memory
            const int batchSize = 100;
            var scrubbed = 0;
            var offset = 0;

            while (true)
            {
                var batch = await db.SyncedFiles
                    .Where(f => f.ExtractedContent != null && f.ExtractedContent != "")
                    .OrderBy(f => f.Id)
                    .Skip(offset)
                    .Take(batchSize)
                    .ToListAsync(cancellationToken);

                if (batch.Count == 0)
                    break;

                foreach (var file in batch)
                {
                    if (string.IsNullOrEmpty(file.ExtractedContent))
                        continue;

                    var original = file.ExtractedContent;
                    var cleaned = original;

                    foreach (var pattern in PiiPatterns)
                    {
                        try
                        {
                            cleaned = pattern.Replace(cleaned, PiiRedaction);
                        }
                        catch (RegexMatchTimeoutException)
                        {
                            logger.LogWarning("PII regex timed out on file {Id}, skipping pattern", file.Id);
                        }
                    }

                    if (cleaned != original)
                    {
                        file.ExtractedContent = cleaned;
                        scrubbed++;
                    }
                }

                await db.SaveChangesAsync(cancellationToken);
                offset += batchSize;
            }

            if (scrubbed > 0)
                logger.LogInformation("Scrubbed PII from {Count} file(s)", scrubbed);
            else
                logger.LogDebug("No PII patterns found in indexed content");
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "PII scrub phase failed");
        }
    }
}
