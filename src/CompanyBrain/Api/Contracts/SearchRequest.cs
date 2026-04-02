namespace CompanyBrain.Api.Contracts;

internal sealed record SearchRequest(string Query, int? MaxResults);