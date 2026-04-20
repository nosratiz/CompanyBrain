using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using CompanyBrain.Dashboard.Middleware;
using CompanyBrain.Dashboard.Services.Dtos;

namespace CompanyBrain.Dashboard.Services;

public sealed class McpStatusClient(HttpClient httpClient)
{
    private const string SessionHeader = "Mcp-Session-Id";

    public async Task<McpServerStatus> GetStatusAsync(CancellationToken ct = default)
    {
        var status = new McpServerStatus();

        try
        {
            var (sessionId, init) = await InitializeSessionAsync(ct);
            if (init?.Result is null)
            {
                status.Error = init?.Error?.Message ?? "No response from MCP server";
                return status;
            }

            PopulateServerInfo(status, init);

            await SendNotificationAsync("notifications/initialized", sessionId, ct);

            var tools = await SendRpcAsync<McpToolsListResult>("tools/list", null, id: 2, sessionId, ct);
            status.Tools = tools?.Result?.Tools ?? [];

            var resources = await SendRpcAsync<McpResourcesListResult>("resources/list", null, id: 3, sessionId, ct);
            status.Resources = resources?.Result?.Resources ?? [];
        }
        catch (UnauthorizedApiException) { throw; }
        catch (Exception ex)
        {
            status.Error = ex.Message;
        }

        return status;
    }

    public async Task<string> CallToolAsync(string toolName, Dictionary<string, object>? arguments, CancellationToken ct = default)
    {
        try
        {
            var (sessionId, _) = await InitializeSessionAsync(ct);
            await SendNotificationAsync("notifications/initialized", sessionId, ct);

            var result = await SendRpcAsync<McpToolCallResult>(
                "tools/call",
                new { name = toolName, arguments = arguments ?? new Dictionary<string, object>() },
                id: 2, sessionId, ct);

            return FormatToolResult(result);
        }
        catch (UnauthorizedApiException) { throw; }
        catch (Exception ex)
        {
            return $"Error: {ex.Message}";
        }
    }

    // ── Private helpers ─────────────────────────────────────────────────

    private async Task<(string? SessionId, JsonRpcResponse<McpInitializeResult>? Init)> InitializeSessionAsync(CancellationToken ct)
    {
        var init = await SendRpcAsync<McpInitializeResult>(
            "initialize",
            new
            {
                protocolVersion = "2025-03-26",
                capabilities = new { },
                clientInfo = new { name = "company-brain-dashboard", version = "1.0.0" },
            },
            id: 1, sessionId: null, ct);

        return (init?.SessionId, init);
    }

    private static void PopulateServerInfo(McpServerStatus status, JsonRpcResponse<McpInitializeResult> init)
    {
        status.IsRunning = true;
        status.ServerName = init.Result!.ServerInfo?.Name;
        status.ServerVersion = init.Result.ServerInfo?.Version;
        status.ProtocolVersion = init.Result.ProtocolVersion;
    }

    private static string FormatToolResult(JsonRpcResponse<McpToolCallResult>? result)
    {
        if (result?.Error is not null)
            return $"Error: {result.Error.Message}";

        if (result?.Result?.Content is not { Count: > 0 })
            return "No result returned.";

        return string.Join("\n", result.Result.Content
            .Where(c => c.Type == "text")
            .Select(c => c.Text ?? ""));
    }

    // ── JSON-RPC helpers ────────────────────────────────────────────────

    private async Task<JsonRpcResponse<T>?> SendRpcAsync<T>(
        string method, object? @params, int id, string? sessionId, CancellationToken ct)
    {
        var body = new { jsonrpc = "2.0", id, method, @params };

        using var req = new HttpRequestMessage(HttpMethod.Post, "/mcp")
        {
            Content = JsonContent.Create(body),
        };
        req.Headers.Accept.ParseAdd("application/json");
        req.Headers.Accept.ParseAdd("text/event-stream");
        if (sessionId is not null)
            req.Headers.TryAddWithoutValidation(SessionHeader, sessionId);

        using var resp = await httpClient.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, ct);
        resp.EnsureSuccessStatusCode();

        var newSession = resp.Headers.TryGetValues(SessionHeader, out var vals)
            ? vals.FirstOrDefault() : sessionId;

        var mediaType = resp.Content.Headers.ContentType?.MediaType;

        JsonRpcResponse<T>? result = mediaType == "text/event-stream"
            ? await ReadSseResponseAsync<T>(resp, ct)
            : await resp.Content.ReadFromJsonAsync<JsonRpcResponse<T>>(ct);

        if (result is not null)
            result.SessionId = newSession;

        return result;
    }

    private async Task SendNotificationAsync(string method, string? sessionId, CancellationToken ct)
    {
        var body = new { jsonrpc = "2.0", method };

        using var req = new HttpRequestMessage(HttpMethod.Post, "/mcp")
        {
            Content = JsonContent.Create(body),
        };
        req.Headers.Accept.ParseAdd("application/json");
        req.Headers.Accept.ParseAdd("text/event-stream");
        if (sessionId is not null)
            req.Headers.TryAddWithoutValidation(SessionHeader, sessionId);

        using var resp = await httpClient.SendAsync(req, ct);
        // Notifications return 202 Accepted or 200 OK – ignore body.
    }

    private static async Task<JsonRpcResponse<T>?> ReadSseResponseAsync<T>(
        HttpResponseMessage resp, CancellationToken ct)
    {
        using var stream = await resp.Content.ReadAsStreamAsync(ct);
        using var reader = new StreamReader(stream);

        while (!ct.IsCancellationRequested)
        {
            var line = await reader.ReadLineAsync(ct);
            if (line is null) break;

            if (line.StartsWith("data: ", StringComparison.Ordinal))
            {
                var json = line.AsSpan()["data: ".Length..];
                var parsed = JsonSerializer.Deserialize<JsonRpcResponse<T>>(json);
                if (parsed?.Id is not null)
                    return parsed;
            }
        }

        return null;
    }
}
