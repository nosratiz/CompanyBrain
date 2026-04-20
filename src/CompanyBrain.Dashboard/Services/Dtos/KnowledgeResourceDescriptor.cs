namespace CompanyBrain.Dashboard.Services.Dtos;

public sealed record KnowledgeResourceDescriptor(
    string Name,
    string? Title,
    string Uri,
    string? Description,
    string? MimeType,
    long? Size);
