using Microsoft.AspNetCore.Http;

namespace CompanyBrain.Dashboard.Mcp.Collections;

public sealed class CollectionAuthorizationHandler(
    CollectionEntitlementsService entitlementsService,
    IHttpContextAccessor httpContextAccessor,
    ILogger<CollectionAuthorizationHandler> logger)
{
    public async Task<(bool IsAllowed, string? Reason)> AuthorizeCollectionAsync(
        string collectionId,
        CancellationToken cancellationToken)
    {
        // IHttpContextAccessor.HttpContext is null in Blazor Server circuit renders.
        // It may also hold a disposed reference from the initial circuit-handshake request.
        // Capture what we need defensively before any await.
        HttpContext? httpContext = null;
        string? team = null;
        try
        {
            httpContext = httpContextAccessor.HttpContext;
            team = httpContext?.Request.Headers["X-Team"].ToString();
        }
        catch (ObjectDisposedException)
        {
            // HttpContext disposed – running inside a Blazor Server circuit. Treat as anonymous.
            httpContext = null;
        }

        var manifest = await entitlementsService.GetManifestAsync(httpContext, cancellationToken);

        var allowed = manifest.CanAccessCollection(collectionId, team);
        if (allowed)
        {
            return (true, null);
        }

        logger.LogInformation(
            "Blocked access to collection '{Collection}' for tier '{Tier}' team '{Team}'",
            collectionId,
            manifest.Tier,
            team ?? "<none>");

        return (false, $"Collection '{collectionId}' is gated by your current license tier.");
    }
}