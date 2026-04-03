using CompanyBrain.MultiTenant.Domain;

namespace CompanyBrain.MultiTenant.Api.Contracts;

// === Tenant Contracts ===

public sealed record CreateTenantRequest(
    string Name,
    string? Description = null,
    TenantPlan Plan = TenantPlan.Free);

public sealed record TenantResponse(
    Guid Id,
    string Name,
    string Slug,
    string? Description,
    TenantStatus Status,
    TenantPlan Plan,
    int MaxDocuments,
    int MaxApiKeys,
    long MaxStorageBytes,
    DateTime CreatedAt,
    int ActiveApiKeys,
    int ActiveUsers);

public sealed record TenantListResponse(IReadOnlyList<TenantSummaryResponse> Tenants);

public sealed record TenantSummaryResponse(
    Guid Id,
    string Name,
    string Slug,
    TenantStatus Status,
    TenantPlan Plan,
    DateTime CreatedAt);

public sealed record UpdateTenantPlanRequest(TenantPlan Plan);

// === API Key Contracts ===

public sealed record CreateApiKeyRequest(
    string Name,
    ApiKeyScope Scope = ApiKeyScope.ReadOnly,
    DateTime? ExpiresAt = null);

public sealed record ApiKeyCreatedResponse(
    Guid Id,
    string Name,
    string KeyPrefix,
    string PlainKey,
    ApiKeyScope Scope,
    DateTime CreatedAt,
    DateTime? ExpiresAt)
{
    public string Warning => "Store this key securely. It will not be shown again.";
}

public sealed record ApiKeyResponse(
    Guid Id,
    string Name,
    string KeyPrefix,
    ApiKeyScope Scope,
    DateTime CreatedAt,
    DateTime? ExpiresAt,
    DateTime? LastUsedAt,
    bool IsRevoked,
    int RequestsPerMinute,
    int RequestsPerDay);

public sealed record ApiKeyListResponse(IReadOnlyList<ApiKeyResponse> ApiKeys);

public sealed record RevokeApiKeyRequest(string? Reason = null);

// === Storage Stats ===

public sealed record TenantStorageStatsResponse(
    int DocumentCount,
    long TotalBytes,
    string FormattedSize,
    long MaxBytes,
    double UsagePercentage);

// === MCP Connection Info ===

public sealed record McpConnectionInfoResponse(
    string ServerUrl,
    string Protocol,
    IReadOnlyList<string> SupportedClients)
{
    public string Instructions => """
        To connect your AI assistant to this MCP server:
        
        1. Copy your API key from the License Management page
        2. Configure your client:
        
        For Claude Desktop (config.json):
        {
          "mcpServers": {
            "company-brain": {
              "url": "<ServerUrl>",
              "headers": {
                "X-API-Key": "YOUR_API_KEY"
              }
            }
          }
        }
        
        For VS Code (mcp.json):
        {
          "servers": {
            "company-brain": {
              "url": "<ServerUrl>",
              "type": "http",
              "headers": {
                "X-API-Key": "YOUR_API_KEY"
              }
            }
          }
        }
        """.Replace("<ServerUrl>", ServerUrl);
}
