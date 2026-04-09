namespace CompanyBrain.Dashboard.Api.Contracts;

internal sealed record IngestResultResponse(string FileName, string ResourceUri, bool ReplacedExisting);
