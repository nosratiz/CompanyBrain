namespace CompanyBrain.Dashboard.Features.Confluence.Models;

public sealed record ConfluencePage(
    string Id,
    string Title,
    string SpaceId,
    string? ParentId,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    int Version,
    string? BodyStorage,
    string WebUrl);
