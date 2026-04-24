using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using FluentResults;

namespace CompanyBrain.Dashboard.Features.Notion.Api;

/// <summary>
/// HTTP client for the Notion REST API v1 (internal integration token auth).
/// All public methods return <see cref="Result{T}"/>; no exceptions are thrown to callers.
/// </summary>
public sealed class NotionApiClient
{
    private const string NotionVersion = "2022-06-28";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly HttpClient _http;
    private readonly ILogger<NotionApiClient> _logger;

    public NotionApiClient(HttpClient http, ILogger<NotionApiClient> logger)
    {
        _http = http;
        _logger = logger;
    }

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>
    /// Searches for pages accessible to the integration.
    /// POST /search
    /// </summary>
    public async Task<Result<NotionSearchResult>> SearchPagesAsync(
        string? query,
        string? cursor,
        CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "v1/search");
        var body = new Dictionary<string, object>
        {
            ["filter"] = new Dictionary<string, string> { ["value"] = "page", ["property"] = "object" },
            ["page_size"] = 100
        };

        if (!string.IsNullOrEmpty(query))
            body["query"] = query;

        if (!string.IsNullOrEmpty(cursor))
            body["start_cursor"] = cursor;

        request.Content = new StringContent(
            JsonSerializer.Serialize(body, JsonOptions),
            Encoding.UTF8,
            "application/json");

        return await ExecuteAsync<NotionSearchResult>(request, ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Returns direct children of a block (page or block).
    /// GET /blocks/{blockId}/children
    /// </summary>
    public async Task<Result<NotionBlockChildrenResult>> GetBlockChildrenAsync(
        string blockId,
        string? cursor,
        CancellationToken ct)
    {
        var url = $"v1/blocks/{Uri.EscapeDataString(blockId)}/children?page_size=100";
        if (!string.IsNullOrEmpty(cursor))
            url += $"&start_cursor={Uri.EscapeDataString(cursor)}";

        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        return await ExecuteAsync<NotionBlockChildrenResult>(request, ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Retrieves the bot user for the current token — used for connection validation.
    /// GET /users/me
    /// </summary>
    public async Task<Result<NotionUserResult>> GetBotUserAsync(CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "v1/users/me");
        return await ExecuteAsync<NotionUserResult>(request, ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Sets the Bearer token on the shared client for the current request scope.
    /// Call this before each public method in the provider (token is resolved per-call).
    /// </summary>
    public void SetToken(string token)
    {
        _http.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private async Task<Result<T>> ExecuteAsync<T>(HttpRequestMessage request, CancellationToken ct)
    {
        request.Headers.Add("Notion-Version", NotionVersion);

        try
        {
            var response = await _http.SendAsync(request, ct).ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                var errorBody = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
                _logger.LogWarning(
                    "Notion API error {Status} for {Method} {Url}: {Body}",
                    (int)response.StatusCode, request.Method, request.RequestUri, errorBody);

                return Result.Fail(
                    $"Notion API returned HTTP {(int)response.StatusCode} ({response.ReasonPhrase})");
            }

            var json = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
            var result = JsonSerializer.Deserialize<T>(json, JsonOptions);

            if (result is null)
                return Result.Fail("Notion API returned an empty or undeserializable response.");

            return Result.Ok(result);
        }
        catch (OperationCanceledException)
        {
            return Result.Fail("Notion API call was cancelled.");
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "HTTP error while calling Notion API");
            return Result.Fail($"HTTP error: {ex.Message}");
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "JSON deserialization failed for Notion API response");
            return Result.Fail($"JSON deserialization error: {ex.Message}");
        }
    }
}
