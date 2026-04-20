using System.Text.Json.Serialization;

namespace CompanyBrain.Dashboard.Services.Dtos;

public sealed class McpToolsListResult
{
    [JsonPropertyName("tools")] public List<McpToolInfo> Tools { get; set; } = [];
}
