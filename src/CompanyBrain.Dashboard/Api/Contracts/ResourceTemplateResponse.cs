namespace CompanyBrain.Dashboard.Api.Contracts;

/// <summary>
/// Response for listing resource templates.
/// </summary>
internal sealed record ResourceTemplateResponse(
    string Name,
    string RepositoryUrl,
    string LocalPath,
    string Branch,
    DateTimeOffset ClonedAt,
    int FileCount,
    IReadOnlyList<string> Files);
