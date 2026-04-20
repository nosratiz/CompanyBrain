using System.Text.Json.Serialization;

namespace CompanyBrain.Dashboard.Services.Dtos;

public sealed record McpServerInfo(
    [property: JsonPropertyName("name")]    string? Name,
    [property: JsonPropertyName("version")] string? Version);
