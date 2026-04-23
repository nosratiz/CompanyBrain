namespace CompanyBrain.Dashboard.Services.Dtos;

public sealed record IngestWikiBatchItemResult(
    string Url,
    string Name,
    string? FileName,
    string? ResourceUri,
    bool Success,
    string? Error);
