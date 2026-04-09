using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace CompanyBrain.Dashboard.Services;

public sealed class McpStatusClient(HttpClient httpClient)
{
    private const string SessionHeader = "Mcp-Session-Id";

    public async Task<McpServerStatus> GetStatusAsync(CancellationToken ct = default)
    {
        var status = new McpServerStatus();

        try
        {
            // Step 1 – Initialize
            var init = await SendRpcAsync<McpInitializeResult>(
                "initialize",
                new
                {
                    protocolVersion = "2025-03-26",
                    capabilities = new { },
                    clientInfo = new { name = "company-brain-dashboard", version = "1.0.0" },
                },
                id: 1, sessionId: null, ct);

            if (init?.Result is null)
            {
                status.Error = init?.Error?.Message ?? "No response from MCP server";
                return status;
            }

            status.IsRunning = true;
            status.ServerName = init.Result.ServerInfo?.Name;
            status.ServerVersion = init.Result.ServerInfo?.Version;
            status.ProtocolVersion = init.Result.ProtocolVersion;

            var sessionId = init.SessionId;

            // Step 2 – Acknowledge initialisation
            await SendNotificationAsync("notifications/initialized", sessionId, ct);

            // Step 3 – List tools
            var tools = await SendRpcAsync<McpToolsListResult>("tools/list", null, id: 2, sessionId, ct);
            status.Tools = tools?.Result?.Tools ?? [];

            // Step 4 – List resources
            var resources = await SendRpcAsync<McpResourcesListResult>("resources/list", null, id: 3, sessionId, ct);
            status.Resources = resources?.Result?.Resources ?? [];
        }
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
            var init = await SendRpcAsync<McpInitializeResult>(
                "initialize",
                new
                {
                    protocolVersion = "2025-03-26",
                    capabilities = new { },
                    clientInfo = new { name = "company-brain-dashboard", version = "1.0.0" },
                },
                id: 1, sessionId: null, ct);

            var sessionId = init?.SessionId;
            await SendNotificationAsync("notifications/initialized", sessionId, ct);

            var result = await SendRpcAsync<McpToolCallResult>(
                "tools/call",
                new { name = toolName, arguments = arguments ?? new Dictionary<string, object>() },
                id: 2, sessionId, ct);

            if (result?.Error is not null)
                return $"Error: {result.Error.Message}";

            if (result?.Result?.Content is { Count: > 0 })
            {
                return string.Join("\n", result.Result.Content
                    .Where(c => c.Type == "text")
                    .Select(c => c.Text ?? ""));
            }

            return "No result returned.";
        }
        catch (Exception ex)
        {
            return $"Error: {ex.Message}";
        }
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

// ── MCP JSON-RPC DTOs ───────────────────────────────────────────────────

public sealed class JsonRpcResponse<T>
{
    [JsonPropertyName("jsonrpc")] public string? Jsonrpc { get; set; }
    [JsonPropertyName("id")]      public int? Id { get; set; }
    [JsonPropertyName("result")]  public T? Result { get; set; }
    [JsonPropertyName("error")]   public JsonRpcError? Error { get; set; }
    [JsonIgnore]                  public string? SessionId { get; set; }
}

public sealed record JsonRpcError(
    [property: JsonPropertyName("code")]    int Code,
    [property: JsonPropertyName("message")] string Message);

public sealed class McpInitializeResult
{
    [JsonPropertyName("protocolVersion")] public string? ProtocolVersion { get; set; }
    [JsonPropertyName("serverInfo")]      public McpServerInfo? ServerInfo { get; set; }
    [JsonPropertyName("capabilities")]    public JsonElement? Capabilities { get; set; }
}

public sealed record McpServerInfo(
    [property: JsonPropertyName("name")]    string? Name,
    [property: JsonPropertyName("version")] string? Version);

public sealed class McpToolsListResult
{
    [JsonPropertyName("tools")] public List<McpToolInfo> Tools { get; set; } = [];
}

public sealed class McpToolInfo
{
    [JsonPropertyName("name")]        public string Name { get; set; } = "";
    [JsonPropertyName("description")] public string? Description { get; set; }
    [JsonPropertyName("inputSchema")] public JsonElement? InputSchema { get; set; }
}

public sealed class McpResourcesListResult
{
    [JsonPropertyName("resources")] public List<McpResourceInfo> Resources { get; set; } = [];
}

public sealed class McpResourceInfo
{
    [JsonPropertyName("name")]        public string Name { get; set; } = "";
    [JsonPropertyName("title")]       public string? Title { get; set; }
    [JsonPropertyName("uri")]         public string Uri { get; set; } = "";
    [JsonPropertyName("description")] public string? Description { get; set; }
    [JsonPropertyName("mimeType")]    public string? MimeType { get; set; }
    [JsonPropertyName("size")]        public long? Size { get; set; }
}

public sealed class McpToolCallResult
{
    [JsonPropertyName("content")] public List<McpToolContent> Content { get; set; } = [];
}

public sealed class McpToolContent
{
    [JsonPropertyName("type")] public string Type { get; set; } = "";
    [JsonPropertyName("text")] public string? Text { get; set; }
}

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
