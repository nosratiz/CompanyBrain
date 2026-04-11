namespace CompanyBrain.Models;

/// <summary>
/// Represents a resource template cloned from a git repository.
/// </summary>
public sealed record ResourceTemplate(
    string Name,
    string RepositoryUrl,
    string LocalPath,
    string Branch,
    DateTimeOffset ClonedAt,
    IReadOnlyList<string> Files);
