using System.Text.Json.Serialization;

namespace CompanyBrain.Dashboard.Services.Dtos;

public sealed class McpResourceInfo
{
    [JsonPropertyName("name")]        public string Name { get; set; } = "";
    [JsonPropertyName("title")]       public string? Title { get; set; }
    [JsonPropertyName("uri")]         public string Uri { get; set; } = "";
    [JsonPropertyName("description")] public string? Description { get; set; }
    [JsonPropertyName("mimeType")]    public string? MimeType { get; set; }
    [JsonPropertyName("size")]        public long? Size { get; set; }
}
