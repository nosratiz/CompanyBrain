namespace CompanyBrain.Dashboard.Api.Contracts;

internal sealed record SearchRequest(string Query, int? MaxResults);
