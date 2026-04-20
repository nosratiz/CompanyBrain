using System.Text.Json.Serialization;

namespace CompanyBrain.Dashboard.Services.Dtos;

public sealed class McpToolCallResult
{
    [JsonPropertyName("content")] public List<McpToolContent> Content { get; set; } = [];
}
