namespace DeepRoot.Shared.Mcp;

/// <summary>
/// Marker abstraction for the background MCP server hosted in-process by
/// the DeepRoot desktop shell. The Photino entry point disposes the
/// implementation when the main window closes.
/// </summary>
public interface IDeepRootMcpServer
{
    Task StartAsync(CancellationToken ct = default);
    Task StopAsync(CancellationToken ct = default);
}
