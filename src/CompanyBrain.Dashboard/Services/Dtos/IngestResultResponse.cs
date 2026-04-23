namespace CompanyBrain.Dashboard.Services.Dtos;

public sealed record IngestResultResponse(
    string FileName,
    string ResourceUri,
    bool ReplacedExisting);
