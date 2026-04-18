namespace CompanyBrain.Dashboard.Api.Contracts;

/// <summary>
/// Response after cloning a git repository as a resource template.
/// </summary>
internal sealed record CloneGitRepositoryResponse(
    string TemplateName,
    string RepositoryUrl,
    string LocalPath,
    string Branch,
    int FileCount,
    bool AlreadyExisted);
