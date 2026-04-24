using System.Collections.Concurrent;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace CompanyBrain.Dashboard.Mcp;

/// <summary>
/// Tracks active MCP server sessions so that REST API endpoints can broadcast
/// <c>notifications/resources/list_changed</c> to all connected MCP clients.
/// </summary>
internal sealed class McpSessionTracker
{
    private readonly ConcurrentDictionary<string, McpServer> _sessions = new();

    public IDisposable Track(McpServer server)
    {
        var key = server.SessionId ?? Guid.NewGuid().ToString("N");
        _sessions.TryAdd(key, server);
        return new Unsubscriber(_sessions, key);
    }

    public int ActiveSessionCount => _sessions.Count;

    public async Task NotifyResourceListChangedAsync(CancellationToken cancellationToken = default)
    {
        foreach (var (key, server) in _sessions)
        {
            try
            {
                await server.SendNotificationAsync(
                    NotificationMethods.ResourceListChangedNotification,
                    cancellationToken);
            }
            catch
            {
                // Session may have been disposed; remove it.
                _sessions.TryRemove(key, out _);
            }
        }
    }

    private sealed class Unsubscriber(ConcurrentDictionary<string, McpServer> sessions, string key) : IDisposable
    {
        public void Dispose() => sessions.TryRemove(key, out _);
    }
}
