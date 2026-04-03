using System.Net.Http.Json;

namespace CompanyBrain.Dashboard.Services;

public sealed class KnowledgeApiClient(HttpClient httpClient)
{
    // === Knowledge Resources ===

    public Task<IReadOnlyList<KnowledgeResourceDescriptor>?> ListResourcesAsync() =>
        httpClient.GetFromJsonAsync<IReadOnlyList<KnowledgeResourceDescriptor>>("/api/knowledge/resources");

    public Task<KnowledgeResourceContent?> GetResourceAsync(string fileName) =>
        httpClient.GetFromJsonAsync<KnowledgeResourceContent>($"/api/knowledge/resources/{Uri.EscapeDataString(fileName)}");

    public Task<SearchResponse?> SearchAsync(string query, int? maxResults = null)
    {
        var url = $"/api/knowledge/search?query={Uri.EscapeDataString(query)}";
        if (maxResults.HasValue)
            url += $"&maxResults={maxResults.Value}";
        return httpClient.GetFromJsonAsync<SearchResponse>(url);
    }

    // === Ingestion ===

    public async Task<IngestResultResponse?> IngestWikiAsync(string url, string? name = null)
    {
        var response = await httpClient.PostAsJsonAsync("/api/knowledge/wiki", new { Url = url, Name = name });
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<IngestResultResponse>();
    }

    public async Task<IngestWikiBatchResponse?> IngestWikiBatchAsync(string url, string? linkSelector = null)
    {
        var response = await httpClient.PostAsJsonAsync("/api/knowledge/wiki/batch", new { Url = url, LinkSelector = linkSelector });
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<IngestWikiBatchResponse>();
    }

    public async Task<IngestResultResponse?> IngestDocumentPathAsync(string filePath, string? name = null)
    {
        var response = await httpClient.PostAsJsonAsync("/api/knowledge/documents/path", new { LocalPath = filePath, Name = name });
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<IngestResultResponse>();
    }
}

// === DTO Records ===

public sealed record KnowledgeResourceDescriptor(
    string FileName,
    string ResourceUri,
    string MimeType,
    long SizeBytes,
    DateTime LastModified);

public sealed record KnowledgeResourceContent(
    string FileName,
    string ResourceUri,
    string MimeType,
    string Content);

public sealed record SearchResponse(
    string Query,
    int MaxResults,
    string Result);

public sealed record SearchMatch(
    string FileName,
    int Score,
    string Snippet);

public sealed record IngestResultResponse(
    string FileName,
    string ResourceUri,
    bool ReplacedExisting);

public sealed record IngestWikiBatchResponse(
    int TotalDiscovered,
    int SuccessfullyIngested,
    int Failed,
    IReadOnlyList<IngestWikiBatchItemResult> Results);

public sealed record IngestWikiBatchItemResult(
    string Url,
    string Name,
    string? FileName,
    string? ResourceUri,
    bool Success,
    string? Error);
