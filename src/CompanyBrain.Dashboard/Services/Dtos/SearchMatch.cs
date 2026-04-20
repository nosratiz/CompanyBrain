namespace CompanyBrain.Dashboard.Services.Dtos;

public sealed record SearchMatch(
    string FileName,
    int Score,
    string Snippet);
