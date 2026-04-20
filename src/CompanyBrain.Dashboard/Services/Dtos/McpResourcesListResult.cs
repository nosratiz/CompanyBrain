using System.Text.Json.Serialization;

namespace CompanyBrain.Dashboard.Services.Dtos;

public sealed class McpResourcesListResult
{
    [JsonPropertyName("resources")] public List<McpResourceInfo> Resources { get; set; } = [];
}
