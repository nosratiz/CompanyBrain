namespace CompanyBrain.Dashboard.Features.Confluence.Models;

public sealed record ConfluenceSpace(
    string Id,
    string Key,
    string Name,
    string? Description,
    string Type,
    string WebUrl);
