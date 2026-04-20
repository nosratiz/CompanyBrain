namespace CompanyBrain.Dashboard.Services.Dtos;

public sealed record ResourceTemplateInfo(
    string Name,
    string RepositoryUrl,
    string LocalPath,
    string Branch,
    DateTimeOffset ClonedAt,
    int FileCount,
    IReadOnlyList<string> Files);
