namespace CompanyBrain.Dashboard.Services.Dtos;

public sealed record IngestWikiBatchResponse(
    int TotalDiscovered,
    int SuccessfullyIngested,
    int Failed,
    IReadOnlyList<IngestWikiBatchItemResult> Results);
