using CompanyBrain.MultiTenant.Abstractions;
using CompanyBrain.MultiTenant.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace CompanyBrain.MultiTenant.Middleware;

/// <summary>
/// Middleware that authenticates requests using API keys and establishes tenant context.
/// For external MCP clients (Claude Desktop, GitHub Copilot, etc.)
/// </summary>
public sealed class ApiKeyAuthenticationMiddleware(
    RequestDelegate next,
    ITenantContextAccessor tenantContextAccessor,
    ILogger<ApiKeyAuthenticationMiddleware> logger)
{
    private const string ApiKeyHeader = "X-API-Key";
    private const string AuthorizationHeader = "Authorization";
    private const string BearerPrefix = "Bearer ";

    public async Task InvokeAsync(HttpContext context, ApiKeyService apiKeyService)
    {
        // Try to extract API key from headers
        var apiKey = ExtractApiKey(context.Request);

        if (string.IsNullOrEmpty(apiKey))
        {
            // No API key provided - could be internal JWT auth, let it pass through
            await next(context);
            return;
        }

        // Validate the API key
        var validationResult = await apiKeyService.ValidateApiKeyAsync(apiKey, context.RequestAborted);

        if (validationResult.IsFailed)
        {
            logger.LogWarning("API key authentication failed: {Error}", string.Join(", ", validationResult.Errors.Select(e => e.Message)));
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync($$"""{"error": "{{validationResult.Errors.First().Message}}"}""", context.RequestAborted);
            return;
        }

        var result = validationResult.Value;

        // Set tenant context for downstream services
        tenantContextAccessor.SetTenant(result.TenantId, result.TenantSlug);

        // Add claims for authorization
        context.Items["TenantId"] = result.TenantId;
        context.Items["TenantSlug"] = result.TenantSlug;
        context.Items["ApiKeyScope"] = result.Scope;

        logger.LogDebug("API key authenticated for tenant {TenantSlug}.", result.TenantSlug);

        try
        {
            await next(context);
        }
        finally
        {
            tenantContextAccessor.Clear();
        }
    }

    private static string? ExtractApiKey(HttpRequest request)
    {
        // Try X-API-Key header first
        if (request.Headers.TryGetValue(ApiKeyHeader, out var apiKeyHeader) &&
            !string.IsNullOrWhiteSpace(apiKeyHeader))
        {
            return apiKeyHeader.ToString();
        }

        // Try Authorization: Bearer header
        if (request.Headers.TryGetValue(AuthorizationHeader, out var authHeader) &&
            authHeader.ToString() is { } auth &&
            auth.StartsWith(BearerPrefix, StringComparison.OrdinalIgnoreCase))
        {
            var token = auth[BearerPrefix.Length..].Trim();
            if (token.StartsWith("cb_")) // Our API key prefix
            {
                return token;
            }
        }

        // Try query string (for SSE connections that can't set headers)
        if (request.Query.TryGetValue("api_key", out var queryKey) &&
            !string.IsNullOrWhiteSpace(queryKey))
        {
            return queryKey.ToString();
        }

        return null;
    }
}
