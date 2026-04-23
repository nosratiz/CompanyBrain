namespace CompanyBrain.Dashboard.Services.Dtos;

public sealed record SearchResponse(
    string Query,
    int MaxResults,
    string Result);
