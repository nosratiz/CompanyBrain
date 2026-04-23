namespace CompanyBrain.Dashboard.Services.Dtos;

public sealed class McpServerStatus
{
    public bool IsRunning { get; set; }
    public string? Error { get; set; }
    public string? ServerName { get; set; }
    public string? ServerVersion { get; set; }
    public string? ProtocolVersion { get; set; }
    public List<McpToolInfo> Tools { get; set; } = [];
    public List<McpResourceInfo> Resources { get; set; } = [];
}
