namespace CompanyBrain.Dashboard.Api.Contracts;

/// <summary>
/// Request to clone a git repository as a resource template.
/// </summary>
internal sealed record CloneGitRepositoryRequest(
    string RepositoryUrl,
    string TemplateName,
    string? Branch);
