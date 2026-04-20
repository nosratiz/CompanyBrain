namespace CompanyBrain.Dashboard.Services.Dtos;

public sealed record CloneGitRepositoryResponse(
    string TemplateName,
    string RepositoryUrl,
    string LocalPath,
    string Branch,
    int FileCount,
    bool AlreadyExisted);
