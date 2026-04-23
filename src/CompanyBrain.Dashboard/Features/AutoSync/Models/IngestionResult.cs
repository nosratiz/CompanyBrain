namespace CompanyBrain.Dashboard.Features.AutoSync.Models;

/// <summary>
/// The outcome of a single ingestion provider run.
/// </summary>
public sealed record IngestionResult
{
    /// <summary>Whether the sync completed without a hard error.</summary>
    public bool Success { get; init; }

    /// <summary>
    /// True when the fetched content differed from the last run, meaning the knowledge
    /// store was updated and (if a vector index is active) re-embedding was triggered.
    /// False means the content hash matched the previous run and no tokens were consumed.
    /// </summary>
    public bool ContentChanged { get; init; }

    /// <summary>
    /// SHA-256 hex digest of the content fetched this run.
    /// Null for providers that manage their own delta logic (e.g. SharePoint Graph delta queries).
    /// </summary>
    public string? ContentHash { get; init; }

    /// <summary>Error description when <see cref="Success"/> is false.</summary>
    public string? ErrorMessage { get; init; }

    // ── Factories ─────────────────────────────────────────────────────────────

    /// <summary>Sync succeeded and content changed (or was newly ingested).</summary>
    public static IngestionResult Succeeded(string? contentHash) =>
        new() { Success = true, ContentChanged = true, ContentHash = contentHash };

    /// <summary>Sync succeeded but content was identical to the last run — embedding skipped.</summary>
    public static IngestionResult Unchanged(string? contentHash) =>
        new() { Success = true, ContentChanged = false, ContentHash = contentHash };

    /// <summary>Sync failed with an error message.</summary>
    public static IngestionResult Failure(string errorMessage) =>
        new() { Success = false, ContentChanged = false, ErrorMessage = errorMessage };
}
