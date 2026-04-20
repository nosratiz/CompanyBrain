using System.Text.Json;
using System.Text.Json.Serialization;

namespace CompanyBrain.Dashboard.Services.Dtos;

public sealed class McpInitializeResult
{
    [JsonPropertyName("protocolVersion")] public string? ProtocolVersion { get; set; }
    [JsonPropertyName("serverInfo")]      public McpServerInfo? ServerInfo { get; set; }
    [JsonPropertyName("capabilities")]    public JsonElement? Capabilities { get; set; }
}
