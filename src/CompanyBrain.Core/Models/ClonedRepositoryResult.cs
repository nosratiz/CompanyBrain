namespace CompanyBrain.Models;

/// <summary>
/// Result of cloning a git repository as a resource template.
/// </summary>
public sealed record ClonedRepositoryResult(
    string TemplateName,
    string RepositoryUrl,
    string LocalPath,
    string Branch,
    int FileCount,
    bool AlreadyExisted);
