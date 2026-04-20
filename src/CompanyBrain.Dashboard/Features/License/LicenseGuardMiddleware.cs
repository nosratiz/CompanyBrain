using CompanyBrain.Dashboard.Features.Auth.Services;

namespace CompanyBrain.Dashboard.Features.License;

/// <summary>
/// Middleware that blocks API/page requests to tier-gated routes when the user's license is insufficient.
/// Runs after authentication so the token is available.
/// </summary>
public sealed class LicenseGuardMiddleware(RequestDelegate next)
{
    // Tier 1+ required (Starter)
    private static readonly string[] Tier1Prefixes =
        ["/sharepoint", "/confluence", "/settings", "/api/sharepoint"];

    // Tier 2+ required (Professional)
    private static readonly string[] Tier2Prefixes =
        ["/tools", "/auto-setup", "/api/setup"];

    public async Task InvokeAsync(HttpContext context)
    {
        var path = context.Request.Path.Value;

        // Only guard authenticated, non-static paths
        if (path is not null)
        {
            var requiredTier = GetRequiredTier(path);

            if (requiredTier > LicenseTier.Free)
            {
                var licenseState = context.RequestServices.GetService<LicenseStateService>();
                var tokenStore = context.RequestServices.GetService<AuthTokenStore>();

                if (tokenStore is { IsAuthenticated: true } && licenseState is not null)
                {
                    await licenseState.LoadAsync(context.RequestAborted);

                    if (licenseState.Tier < requiredTier)
                    {
                        // For API calls, return 403
                        if (path.StartsWith("/api/", StringComparison.OrdinalIgnoreCase))
                        {
                            context.Response.StatusCode = StatusCodes.Status403Forbidden;
                            await context.Response.WriteAsJsonAsync(new
                            {
                                error = "License upgrade required",
                                requiredTier = requiredTier.ToString(),
                                currentTier = licenseState.Tier.ToString()
                            }, context.RequestAborted);
                            return;
                        }

                        // For page navigations, redirect to dashboard
                        context.Response.Redirect("/");
                        return;
                    }
                }
            }
        }

        await next(context);
    }

    private static LicenseTier GetRequiredTier(string path)
    {
        foreach (var prefix in Tier2Prefixes)
        {
            if (path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                return LicenseTier.Professional;
        }

        foreach (var prefix in Tier1Prefixes)
        {
            if (path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                return LicenseTier.Starter;
        }

        return LicenseTier.Free;
    }
}
