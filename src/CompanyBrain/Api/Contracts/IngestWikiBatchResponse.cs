namespace CompanyBrain.Api.Contracts;

/// <summary>
/// Response for batch wiki ingestion.
/// </summary>
/// <param name="TotalDiscovered">Total number of wiki links discovered.</param>
/// <param name="SuccessfullyIngested">Number of documents successfully ingested.</param>
/// <param name="Failed">Number of documents that failed to ingest.</param>
/// <param name="Results">Individual results for each document.</param>
internal sealed record IngestWikiBatchResponse(
    int TotalDiscovered,
    int SuccessfullyIngested,
    int Failed,
    IReadOnlyList<IngestWikiBatchItemResult> Results);

/// <summary>
/// Result for a single document in the batch.
/// </summary>
/// <param name="Url">The source URL.</param>
/// <param name="Name">The document name.</param>
/// <param name="FileName">The saved file name, if successful.</param>
/// <param name="ResourceUri">The resource URI, if successful.</param>
/// <param name="Success">Whether ingestion succeeded.</param>
/// <param name="Error">Error message if failed.</param>
internal sealed record IngestWikiBatchItemResult(
    string Url,
    string Name,
    string? FileName,
    string? ResourceUri,
    bool Success,
    string? Error);
