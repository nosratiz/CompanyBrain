using System.Net.Http.Json;
using CompanyBrain.Dashboard.Features.DocumentTenant.Requests;
using CompanyBrain.Dashboard.Features.DocumentTenant.Responses;
using CompanyBrain.Dashboard.Middleware;
using CompanyBrain.Dashboard.Services.Dtos;

namespace CompanyBrain.Dashboard.Services;

/// <summary>
/// Client for managing document-tenant assignments via the internal API.
/// </summary>
public sealed class DocumentTenantApiClient(HttpClient httpClient)
{
    /// <summary>
    /// Gets all tenant assignments for a specific document.
    /// </summary>
    public async Task<DocumentAssignmentsResponse?> GetAssignmentsByDocumentAsync(string fileName, CancellationToken ct = default)
    {
        try
        {
            return await httpClient.GetFromJsonAsync<DocumentAssignmentsResponse>(
                $"/api/document-tenants/by-document/{Uri.EscapeDataString(fileName)}", ct);
        }
        catch (UnauthorizedApiException) { throw; }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Updates tenant assignments for a document. Pass empty list to make document available to everyone.
    /// </summary>
    public async Task<DocumentAssignmentsResponse?> UpdateDocumentTenantsAsync(
        string fileName,
        IReadOnlyList<TenantAssignment> tenants,
        CancellationToken ct = default)
    {
        try
        {
            var request = new UpdateDocumentTenantsRequest(fileName, tenants);
            var response = await httpClient.PutAsJsonAsync(
                $"/api/document-tenants/by-document/{Uri.EscapeDataString(fileName)}",
                request,
                ct);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<DocumentAssignmentsResponse>(cancellationToken: ct);
        }
        catch (UnauthorizedApiException) { throw; }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Assigns a document to a tenant.
    /// </summary>
    public async Task<DocumentTenantAssignmentResponse?> AssignDocumentToTenantAsync(
        string fileName,
        Guid tenantId,
        string tenantName,
        CancellationToken ct = default)
    {
        try
        {
            var request = new AssignDocumentToTenantRequest(fileName, tenantId, tenantName);
            var response = await httpClient.PostAsJsonAsync("/api/document-tenants", request, ct);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<DocumentTenantAssignmentResponse>(cancellationToken: ct);
        }
        catch (UnauthorizedApiException) { throw; }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Removes a specific tenant assignment from a document.
    /// </summary>
    public async Task<bool> RemoveAssignmentAsync(string fileName, Guid tenantId, CancellationToken ct = default)
    {
        try
        {
            var response = await httpClient.DeleteAsync(
                $"/api/document-tenants/by-document/tenant/{tenantId}/{Uri.EscapeDataString(fileName)}",
                ct);
            return response.IsSuccessStatusCode;
        }
        catch (UnauthorizedApiException) { throw; }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Gets all available tenants from the external API.
    /// </summary>
    public async Task<IReadOnlyList<TenantSummaryDto>?> GetAvailableTenantsAsync(CancellationToken ct = default)
    {
        try
        {
            return await httpClient.GetFromJsonAsync<IReadOnlyList<TenantSummaryDto>>(
                "/api/document-tenants/available-tenants", ct);
        }
        catch (UnauthorizedApiException) { throw; }
        catch
        {
            return [];
        }
    }
}
