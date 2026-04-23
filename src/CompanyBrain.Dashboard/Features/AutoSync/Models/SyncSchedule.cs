namespace CompanyBrain.Dashboard.Features.AutoSync.Models;

/// <summary>
/// Represents a recurring sync job that ingests content from an external URL
/// into a DeepRoot knowledge collection on a cron-based schedule.
/// </summary>
public sealed class SyncSchedule
{
    /// <summary>Auto-increment primary key.</summary>
    public int Id { get; set; }

    /// <summary>The URL endpoint to scrape/sync (web page, SharePoint site, Confluence space key, etc.).</summary>
    public required string SourceUrl { get; set; }

    /// <summary>The external platform this schedule targets.</summary>
    public SourceType SourceType { get; set; }

    /// <summary>
    /// The DeepRoot knowledge collection to write ingested content into.
    /// Defaults to "General" when null or empty.
    /// </summary>
    public string? CollectionName { get; set; }

    /// <summary>
    /// Standard 5-field cron expression (e.g. <c>0 2 * * *</c> for 02:00 UTC daily).
    /// Parsed by <see cref="Services.CronEvaluator"/> using the Cronos library.
    /// </summary>
    public required string CronExpression { get; set; }

    /// <summary>UTC timestamp of the last successful sync. Null means never synced.</summary>
    public DateTime? LastSyncUtc { get; set; }

    /// <summary>
    /// SHA-256 hex digest of the last fetched content, used for delta detection.
    /// When the hash matches the freshly fetched content the embedding step is skipped,
    /// avoiding unnecessary LLM/embedding API calls.
    /// </summary>
    public string? LastContentHash { get; set; }

    /// <summary>Whether this schedule should be evaluated by the worker loop.</summary>
    public bool IsActive { get; set; } = true;

    /// <summary>Human-readable error from the most recent failed attempt. Cleared on success.</summary>
    public string? LastErrorMessage { get; set; }

    /// <summary>Number of consecutive failures since the last success. Used to compute back-off delays.</summary>
    public int ConsecutiveFailureCount { get; set; }

    /// <summary>
    /// When back-off is active, the earliest UTC timestamp at which the schedule should
    /// be retried. The worker skips the schedule until <see cref="DateTime.UtcNow"/> exceeds this value.
    /// </summary>
    public DateTime? NextRetryUtc { get; set; }

    /// <summary>UTC timestamp when this record was first created.</summary>
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
