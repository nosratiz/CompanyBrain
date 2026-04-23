using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using CompanyBrain.Dashboard.Features.Confluence.Models;

namespace CompanyBrain.Dashboard.Features.Confluence.Services;

/// <summary>
/// HTTP client for the Confluence Cloud REST API v2.
/// Uses Basic auth (email + API token).
/// </summary>
public sealed class ConfluenceApiService(
    HttpClient httpClient,
    ConfluenceSettingsProvider settingsProvider,
    ILogger<ConfluenceApiService> logger)
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private async Task ConfigureClientAsync(CancellationToken cancellationToken)
    {
        var opts = await settingsProvider.GetEffectiveOptionsAsync(cancellationToken);

        if (string.IsNullOrEmpty(opts.Domain) || string.IsNullOrEmpty(opts.Email) || string.IsNullOrEmpty(opts.ApiToken))
            throw new InvalidOperationException("Confluence credentials are not configured.");

        var credentials = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{opts.Email}:{opts.ApiToken}"));
        httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", credentials);
        httpClient.BaseAddress = new Uri($"https://{opts.Domain}.atlassian.net/wiki/api/v2/");
    }

    /// <summary>
    /// Lists all global and personal spaces. Follows pagination automatically.
    /// </summary>
    public async Task<List<ConfluenceSpace>> GetSpacesAsync(CancellationToken cancellationToken = default)
    {
        await ConfigureClientAsync(cancellationToken);

        var spaces = new List<ConfluenceSpace>();
        var cursor = (string?)null;

        do
        {
            var url = cursor is null
                ? "spaces?limit=50&status=current"
                : $"spaces?limit=50&status=current&cursor={Uri.EscapeDataString(cursor)}";

            var response = await httpClient.GetAsync(url, cancellationToken);
            response.EnsureSuccessStatusCode();

            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            var page = JsonSerializer.Deserialize<PagedResult<SpaceDto>>(body, JsonOptions);

            if (page?.Results is null) break;

            foreach (var dto in page.Results)
            {
                var opts = await settingsProvider.GetEffectiveOptionsAsync(cancellationToken);
                spaces.Add(new ConfluenceSpace(
                    dto.Id,
                    dto.Key ?? string.Empty,
                    dto.Name ?? string.Empty,
                    dto.Description?.Plain?.Value,
                    dto.Type ?? "global",
                    $"https://{opts.Domain}.atlassian.net/wiki{dto.Links?.WebUi ?? $"/spaces/{dto.Key}"}"));
            }

            cursor = ExtractNextCursor(page.Links?.Next);

        } while (cursor is not null);

        logger.LogDebug("Fetched {Count} Confluence spaces", spaces.Count);
        return spaces;
    }

    /// <summary>
    /// Returns all pages in a space. Fetches body in storage format for conversion.
    /// </summary>
    public async Task<List<ConfluencePage>> GetAllPagesAsync(string spaceId, CancellationToken cancellationToken = default)
    {
        await ConfigureClientAsync(cancellationToken);

        var pages = new List<ConfluencePage>();
        var cursor = (string?)null;

        do
        {
            var url = cursor is null
                ? $"pages?space-id={spaceId}&body-format=storage&limit=50&status=current"
                : $"pages?space-id={spaceId}&body-format=storage&limit=50&status=current&cursor={Uri.EscapeDataString(cursor)}";

            var response = await httpClient.GetAsync(url, cancellationToken);
            response.EnsureSuccessStatusCode();

            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            var page = JsonSerializer.Deserialize<PagedResult<PageDto>>(body, JsonOptions);

            if (page?.Results is null) break;

            foreach (var dto in page.Results)
                pages.Add(MapPage(dto));

            cursor = ExtractNextCursor(page.Links?.Next);

        } while (cursor is not null);

        logger.LogDebug("Fetched {Count} pages for space {SpaceId}", pages.Count, spaceId);
        return pages;
    }

    /// <summary>
    /// Fetches a single page including its storage-format body.
    /// </summary>
    public async Task<ConfluencePage?> GetPageAsync(string pageId, CancellationToken cancellationToken = default)
    {
        await ConfigureClientAsync(cancellationToken);

        var response = await httpClient.GetAsync($"pages/{pageId}?body-format=storage", cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            logger.LogWarning("Failed to get page {PageId}: {Status}", pageId, response.StatusCode);
            return null;
        }

        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        var dto = JsonSerializer.Deserialize<PageDto>(body, JsonOptions);
        return dto is null ? null : MapPage(dto);
    }

    /// <summary>
    /// Verifies connectivity and authentication against the Confluence API.
    /// </summary>
    public async Task<(bool Success, string? Error)> TestConnectionAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await ConfigureClientAsync(cancellationToken);
            var response = await httpClient.GetAsync("spaces?limit=1", cancellationToken);
            if (response.IsSuccessStatusCode)
                return (true, null);

            var err = await response.Content.ReadAsStringAsync(cancellationToken);
            return (false, $"HTTP {(int)response.StatusCode}: {response.ReasonPhrase}");
        }
        catch (Exception ex)
        {
            return (false, ex.Message);
        }
    }

    private static ConfluencePage MapPage(PageDto dto)
    {
        var version = dto.Version?.Number ?? 1;
        var updatedAt = dto.Version?.CreatedAt ?? dto.CreatedAt ?? DateTimeOffset.UtcNow;

        return new ConfluencePage(
            dto.Id ?? string.Empty,
            dto.Title ?? "Untitled",
            dto.SpaceId ?? string.Empty,
            dto.ParentId,
            dto.CreatedAt ?? DateTimeOffset.UtcNow,
            updatedAt,
            version,
            dto.Body?.Storage?.Value,
            dto.Links?.WebUi ?? string.Empty);
    }

    private static string? ExtractNextCursor(string? nextLink)
    {
        if (string.IsNullOrEmpty(nextLink)) return null;
        var idx = nextLink.IndexOf("cursor=", StringComparison.Ordinal);
        if (idx < 0) return null;
        var raw = nextLink[(idx + 7)..];
        var end = raw.IndexOf('&');
        return end < 0 ? Uri.UnescapeDataString(raw) : Uri.UnescapeDataString(raw[..end]);
    }

    // ── Internal DTO types ───────────────────────────────────────────────────

    private sealed class PagedResult<T>
    {
        [JsonPropertyName("results")] public List<T>? Results { get; set; }
        [JsonPropertyName("_links")] public PagingLinks? Links { get; set; }
    }

    private sealed class PagingLinks
    {
        [JsonPropertyName("next")] public string? Next { get; set; }
        [JsonPropertyName("webui")] public string? WebUi { get; set; }
    }

    private sealed class SpaceDto
    {
        [JsonPropertyName("id")] public string Id { get; set; } = string.Empty;
        [JsonPropertyName("key")] public string? Key { get; set; }
        [JsonPropertyName("name")] public string? Name { get; set; }
        [JsonPropertyName("type")] public string? Type { get; set; }
        [JsonPropertyName("description")] public SpaceDescriptionDto? Description { get; set; }
        [JsonPropertyName("_links")] public PagingLinks? Links { get; set; }
    }

    private sealed class SpaceDescriptionDto
    {
        [JsonPropertyName("plain")] public SpaceDescriptionPlain? Plain { get; set; }
    }

    private sealed class SpaceDescriptionPlain
    {
        [JsonPropertyName("value")] public string? Value { get; set; }
    }

    private sealed class PageDto
    {
        [JsonPropertyName("id")] public string? Id { get; set; }
        [JsonPropertyName("title")] public string? Title { get; set; }
        [JsonPropertyName("spaceId")] public string? SpaceId { get; set; }
        [JsonPropertyName("parentId")] public string? ParentId { get; set; }
        [JsonPropertyName("createdAt")] public DateTimeOffset? CreatedAt { get; set; }
        [JsonPropertyName("version")] public PageVersionDto? Version { get; set; }
        [JsonPropertyName("body")] public PageBodyDto? Body { get; set; }
        [JsonPropertyName("_links")] public PagingLinks? Links { get; set; }
    }

    private sealed class PageVersionDto
    {
        [JsonPropertyName("number")] public int Number { get; set; }
        [JsonPropertyName("createdAt")] public DateTimeOffset? CreatedAt { get; set; }
    }

    private sealed class PageBodyDto
    {
        [JsonPropertyName("storage")] public PageBodyContentDto? Storage { get; set; }
    }

    private sealed class PageBodyContentDto
    {
        [JsonPropertyName("value")] public string? Value { get; set; }
    }
}
