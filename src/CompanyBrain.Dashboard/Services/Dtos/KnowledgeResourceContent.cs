namespace CompanyBrain.Dashboard.Services.Dtos;

public sealed record KnowledgeResourceContent(
    string FileName,
    string Uri,
    string? MimeType,
    string Content);
