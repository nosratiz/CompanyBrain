using System.Text;
using System.Text.Json;
using CompanyBrain.Dashboard.Features.License;

namespace CompanyBrain.Dashboard.Mcp.Collections;

/// <summary>
/// HTTP-level guard for MCP resource reads.
/// Enforces collection tier gates before the MCP handler pipeline executes.
/// </summary>
public sealed class McpCollectionLicenseMiddleware(RequestDelegate next)
{
    private const string CollectionPrefix = "mcp://internal/knowledge/";

    public async Task InvokeAsync(HttpContext context)
    {
        if (!context.Request.Path.StartsWithSegments("/mcp", StringComparison.OrdinalIgnoreCase)
            || !HttpMethods.IsPost(context.Request.Method))
        {
            await next(context);
            return;
        }

        var resourceUri = await TryReadMcpResourceUriAsync(context, context.RequestAborted);
        if (string.IsNullOrWhiteSpace(resourceUri)
            || !resourceUri.StartsWith(CollectionPrefix, StringComparison.OrdinalIgnoreCase))
        {
            await next(context);
            return;
        }

        var collectionId = resourceUri[CollectionPrefix.Length..]
            .Split('/', StringSplitOptions.RemoveEmptyEntries)
            .FirstOrDefault();

        if (string.IsNullOrWhiteSpace(collectionId))
        {
            await next(context);
            return;
        }

        if (IsAllowed(collectionId, context))
        {
            await next(context);
            return;
        }

        context.Response.StatusCode = StatusCodes.Status403Forbidden;
        await context.Response.WriteAsJsonAsync(new
        {
            error = "Collection access denied by license tier",
            collection = collectionId
        }, context.RequestAborted);
    }

    private static bool IsAllowed(string collectionId, HttpContext context)
    {
        if (string.Equals(collectionId, "General", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var tierHeader = context.Request.Headers["X-License-Tier"].ToString();
        var tier = tierHeader.ToLowerInvariant() switch
        {
            "enterprise" => LicenseTier.Enterprise,
            "pro" or "professional" => LicenseTier.Professional,
            "starter" => LicenseTier.Starter,
            _ => LicenseTier.Free,
        };

        return tier switch
        {
            LicenseTier.Free => false,
            LicenseTier.Professional => IsAllowedForPro(collectionId, context),
            LicenseTier.Enterprise => true,
            _ => false,
        };
    }

    private static bool IsAllowedForPro(string collectionId, HttpContext context)
    {
        var allowedHeader = context.Request.Headers["X-Allowed-Collections"].ToString();
        if (string.IsNullOrWhiteSpace(allowedHeader))
        {
            return false;
        }

        var allowed = allowedHeader
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(3)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        return allowed.Contains(collectionId);
    }

    private static async Task<string?> TryReadMcpResourceUriAsync(HttpContext context, CancellationToken cancellationToken)
    {
        context.Request.EnableBuffering();
        using var reader = new StreamReader(context.Request.Body, Encoding.UTF8, leaveOpen: true);
        var body = await reader.ReadToEndAsync(cancellationToken);
        context.Request.Body.Position = 0;

        if (string.IsNullOrWhiteSpace(body))
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(body);
            var root = document.RootElement;
            if (!root.TryGetProperty("method", out var method))
            {
                return null;
            }

            var methodName = method.GetString();
            if (string.IsNullOrWhiteSpace(methodName)
                || !methodName.Contains("resources/read", StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            if (!root.TryGetProperty("params", out var parameters)
                || !parameters.TryGetProperty("uri", out var uriProperty))
            {
                return null;
            }

            return uriProperty.GetString();
        }
        catch
        {
            return null;
        }
    }
}
