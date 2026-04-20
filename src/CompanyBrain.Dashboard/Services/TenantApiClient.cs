using System.Net.Http.Json;
using CompanyBrain.Dashboard.Middleware;
using CompanyBrain.Dashboard.Services.Dtos;

namespace CompanyBrain.Dashboard.Services;

public sealed class KnowledgeApiClient(HttpClient httpClient)
{
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

    public async Task<SearchResponse?> SearchAsync(string query, int? maxResults = null)
    {
        var url = $"/api/knowledge/search?query={Uri.EscapeDataString(query)}";
        if (maxResults.HasValue)
            url += $"&maxResults={maxResults.Value}";
        try
        {
            return await httpClient.GetFromJsonAsync<SearchResponse>(url);
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

    public async Task<IngestResultResponse?> UploadDocumentAsync(Stream fileStream, string fileName, string? name = null)
    {
        using var content = new MultipartFormDataContent();
        content.Add(new StreamContent(fileStream), "file", fileName);
        if (!string.IsNullOrWhiteSpace(name))
            content.Add(new StringContent(name), "name");

        var response = await httpClient.PostAsync("/api/knowledge/documents/upload", content);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<IngestResultResponse>();
    }

    public async Task<bool> DeleteResourceAsync(string fileName)
    {
        var response = await httpClient.DeleteAsync($"/api/knowledge/resources/{Uri.EscapeDataString(fileName)}");
        return response.IsSuccessStatusCode;
    }

    public async Task<IngestResultResponse?> IngestDatabaseSchemaAsync(string connectionString, string name, string provider)
    {
        var response = await httpClient.PostAsJsonAsync("/api/knowledge/database-schema",
            new { ConnectionString = connectionString, Name = name, Provider = provider });
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
