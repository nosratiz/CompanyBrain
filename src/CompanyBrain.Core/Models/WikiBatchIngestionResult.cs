namespace CompanyBrain.Models;

/// <summary>
/// Result of a batch wiki ingestion operation.
/// </summary>
/// <param name="TotalDiscovered">Total number of wiki links discovered.</param>
/// <param name="SuccessfullyIngested">Number of documents successfully ingested.</param>
/// <param name="Failed">Number of documents that failed to ingest.</param>
/// <param name="Results">Individual results for each document.</param>
public sealed record WikiBatchIngestionResult(
    int TotalDiscovered,
    int SuccessfullyIngested,
    int Failed,
    IReadOnlyList<WikiBatchIngestionItemResult> Results);

/// <summary>
/// Result for a single document in the batch.
/// </summary>
/// <param name="Url">The source URL.</param>
/// <param name="Name">The document name.</param>
/// <param name="FileName">The saved file name, if successful.</param>
/// <param name="ResourceUri">The resource URI, if successful.</param>
/// <param name="Success">Whether ingestion succeeded.</param>
/// <param name="Error">Error message if failed.</param>
public sealed record WikiBatchIngestionItemResult(
    string Url,
    string Name,
    string? FileName,
    string? ResourceUri,
    bool Success,
    string? Error);
