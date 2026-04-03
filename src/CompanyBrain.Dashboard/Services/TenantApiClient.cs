using System.Net.Http.Json;

namespace CompanyBrain.Dashboard.Services;

public sealed class KnowledgeApiClient(HttpClient httpClient)
{
    // === Knowledge Resources ===

    public Task<IReadOnlyList<KnowledgeResourceDescriptor>?> ListResourcesAsync() =>
        httpClient.GetFromJsonAsync<IReadOnlyList<KnowledgeResourceDescriptor>>("/api/knowledge/resources");

    public Task<KnowledgeResourceContent?> GetResourceAsync(string fileName) =>
        httpClient.GetFromJsonAsync<KnowledgeResourceContent>($"/api/knowledge/resources/{Uri.EscapeDataString(fileName)}");

    public Task<SearchResponse?> SearchAsync(string query) =>
        httpClient.GetFromJsonAsync<SearchResponse>($"/api/knowledge/search?query={Uri.EscapeDataString(query)}");

    // === Ingestion ===

    public async Task<IngestResultResponse?> IngestWikiAsync(string url, string name)
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
        var response = await httpClient.PostAsJsonAsync("/api/knowledge/documents/path", new { FilePath = filePath, Name = name });
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
    IReadOnlyList<SearchMatch> Matches);

public sealed record SearchMatch(
    string FileName,
    string ResourceUri,
    string Snippet,
    double Score);

public sealed record IngestResultResponse(
    string FileName,
    string ResourceUri,
    bool Existed);

public sealed record IngestWikiBatchResponse(
    int TotalDiscovered,
    int SuccessCount,
    int FailedCount,
    IReadOnlyList<IngestWikiBatchItemResult> Results);

public sealed record IngestWikiBatchItemResult(
    string Url,
    bool Success,
    string? FileName,
    string? ResourceUri,
    string? Error);
