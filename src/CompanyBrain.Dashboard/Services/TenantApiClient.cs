using System.Net.Http.Json;
using CompanyBrain.Dashboard.Features.Auth.Services;
using CompanyBrain.Dashboard.Middleware;
using CompanyBrain.Dashboard.Services.Dtos;

namespace CompanyBrain.Dashboard.Services;

internal sealed class KnowledgeApiClient(HttpClient httpClient, AuthTokenStore tokenStore)
{
    // Builds a request and injects X-Actor-Email so the server can attribute audit log entries.
    private HttpRequestMessage BuildRequest(HttpMethod method, string url, object? body = null)
    {
        var request = new HttpRequestMessage(method, url);
        if (body is not null)
            request.Content = JsonContent.Create(body);
        if (!string.IsNullOrEmpty(tokenStore.Email))
            request.Headers.TryAddWithoutValidation("X-Actor-Email", tokenStore.Email);
        return request;
    }

    // === Knowledge Resources ===

    public async Task<IReadOnlyList<KnowledgeResourceDescriptor>?> ListResourcesAsync()
    {
        try
        {
            return await httpClient.GetFromJsonAsync<IReadOnlyList<KnowledgeResourceDescriptor>>("/api/knowledge/resources");
        }
        catch (UnauthorizedApiException) { throw; }
        catch
        {
            return [];
        }
    }

    public async Task<KnowledgeResourceContent?> GetResourceAsync(string fileName)
    {
        try
        {
            return await httpClient.GetFromJsonAsync<KnowledgeResourceContent>($"/api/knowledge/resources/{Uri.EscapeDataString(fileName)}");
        }
        catch (UnauthorizedApiException) { throw; }
        catch
        {
            return null;
        }
    }

    public async Task<SearchResponse?> SearchAsync(string query, int? maxResults = null, string? collectionId = null)
    {
        var url = $"/api/knowledge/search?query={Uri.EscapeDataString(query)}";
        if (maxResults.HasValue)
            url += $"&maxResults={maxResults.Value}";
        if (!string.IsNullOrWhiteSpace(collectionId))
            url += $"&collectionId={Uri.EscapeDataString(collectionId)}";
        try
        {
            using var request = BuildRequest(HttpMethod.Get, url);
            var response = await httpClient.SendAsync(request);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<SearchResponse>();
        }
        catch (UnauthorizedApiException) { throw; }
        catch
        {
            return null;
        }
    }

    // === Ingestion ===

    public async Task<IngestResultResponse?> IngestWikiAsync(string url, string? name = null)
    {
        using var request = BuildRequest(HttpMethod.Post, "/api/knowledge/wiki", new { Url = url, Name = name });
        var response = await httpClient.SendAsync(request);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<IngestResultResponse>();
    }

    public async Task<IngestWikiBatchResponse?> IngestWikiBatchAsync(string url, string? linkSelector = null)
    {
        using var request = BuildRequest(HttpMethod.Post, "/api/knowledge/wiki/batch", new { Url = url, LinkSelector = linkSelector });
        var response = await httpClient.SendAsync(request);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<IngestWikiBatchResponse>();
    }

    public async Task<IngestResultResponse?> IngestDocumentPathAsync(string filePath, string? name = null)
    {
        using var request = BuildRequest(HttpMethod.Post, "/api/knowledge/documents/path", new { LocalPath = filePath, Name = name });
        var response = await httpClient.SendAsync(request);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<IngestResultResponse>();
    }

    public async Task<IngestResultResponse?> UploadDocumentAsync(Stream fileStream, string fileName, string? name = null, string? collectionId = null)
    {
        using var content = new MultipartFormDataContent();
        content.Add(new StreamContent(fileStream), "file", fileName);
        if (!string.IsNullOrWhiteSpace(name))
            content.Add(new StringContent(name), "name");
        if (!string.IsNullOrWhiteSpace(collectionId))
            content.Add(new StringContent(collectionId), "collectionId");

        using var message = new HttpRequestMessage(HttpMethod.Post, "/api/knowledge/documents/upload");
        message.Content = content;
        if (!string.IsNullOrEmpty(tokenStore.Email))
            message.Headers.TryAddWithoutValidation("X-Actor-Email", tokenStore.Email);

        var response = await httpClient.SendAsync(message);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<IngestResultResponse>();
    }

    public async Task<bool> DeleteResourceAsync(string fileName)
    {
        using var request = BuildRequest(HttpMethod.Delete, $"/api/knowledge/resources/{Uri.EscapeDataString(fileName)}");
        var response = await httpClient.SendAsync(request);
        return response.IsSuccessStatusCode;
    }

    public async Task<IngestResultResponse?> IngestDatabaseSchemaAsync(string connectionString, string name, string provider)
    {
        using var request = BuildRequest(HttpMethod.Post, "/api/knowledge/database-schema",
            new { ConnectionString = connectionString, Name = name, Provider = provider });
        var response = await httpClient.SendAsync(request);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<IngestResultResponse>();
    }

    // === Resource Templates ===

    public async Task<CloneGitRepositoryResponse?> CloneGitRepositoryAsync(string repositoryUrl, string templateName, string? branch = null)
    {
        var response = await httpClient.PostAsJsonAsync("/api/templates/clone",
            new { RepositoryUrl = repositoryUrl, TemplateName = templateName, Branch = branch });
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<CloneGitRepositoryResponse>();
    }

    public async Task<IReadOnlyList<ResourceTemplateInfo>?> ListTemplatesAsync()
    {
        try
        {
            return await httpClient.GetFromJsonAsync<IReadOnlyList<ResourceTemplateInfo>>("/api/templates");
        }
        catch (UnauthorizedApiException) { throw; }
        catch
        {
            return [];
        }
    }

    public async Task<string?> GetTemplateFileAsync(string templateName, string relativePath)
    {
        try
        {
            return await httpClient.GetStringAsync($"/api/templates/{Uri.EscapeDataString(templateName)}/files/{relativePath}");
        }
        catch (UnauthorizedApiException) { throw; }
        catch
        {
            return null;
        }
    }

    public async Task<bool> DeleteTemplateAsync(string templateName)
    {
        var response = await httpClient.DeleteAsync($"/api/templates/{Uri.EscapeDataString(templateName)}");
        return response.IsSuccessStatusCode;
    }
}
