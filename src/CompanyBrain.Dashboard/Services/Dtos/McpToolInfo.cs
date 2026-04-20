using System.Text.Json;
using System.Text.Json.Serialization;

namespace CompanyBrain.Dashboard.Services.Dtos;

public sealed class McpToolInfo
{
    [JsonPropertyName("name")]        public string Name { get; set; } = "";
    [JsonPropertyName("description")] public string? Description { get; set; }
    [JsonPropertyName("inputSchema")] public JsonElement? InputSchema { get; set; }
}
