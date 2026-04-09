namespace CompanyBrain.Dashboard.Api.Contracts;

internal sealed record SearchResponse(string Query, int MaxResults, string Result);
