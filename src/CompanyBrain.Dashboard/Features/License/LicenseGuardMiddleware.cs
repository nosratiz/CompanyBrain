using CompanyBrain.Dashboard.Features.Auth.Services;

namespace CompanyBrain.Dashboard.Features.License;

/// <summary>
/// Middleware that blocks API/page requests to tier-gated routes when the user's license is insufficient.
/// Runs after authentication so the token is available.
/// </summary>
public sealed class LicenseGuardMiddleware(RequestDelegate next)
{
    private static readonly string[] Tier1Prefixes =
        ["/sharepoint", "/confluence", "/settings", "/api/sharepoint"];

    private static readonly string[] Tier2Prefixes =
        ["/tools", "/auto-setup", "/api/setup"];

    public async Task InvokeAsync(HttpContext context)
    {
        var path = context.Request.Path.Value;

        if (path is null || !RequiresLicenseCheck(path, context, out var requiredTier, out var licenseState))
        {
            await next(context);
            return;
        }

        await licenseState!.LoadAsync(context.RequestAborted);

        if (licenseState.Tier >= requiredTier)
        {
            await next(context);
            return;
        }

        await WriteForbiddenResponseAsync(context, path, requiredTier, licenseState.Tier);
    }

    private static bool RequiresLicenseCheck(
        string path,
        HttpContext context,
        out LicenseTier requiredTier,
        out LicenseStateService? licenseState)
    {
        licenseState = null;
        requiredTier = GetRequiredTier(path);

        if (requiredTier <= LicenseTier.Free)
            return false;

        var tokenStore = context.RequestServices.GetService<AuthTokenStore>();
        if (tokenStore is not { IsAuthenticated: true })
            return false;

        licenseState = context.RequestServices.GetService<LicenseStateService>();
        return licenseState is not null;
    }

    private static async Task WriteForbiddenResponseAsync(
        HttpContext context,
        string path,
        LicenseTier requiredTier,
        LicenseTier currentTier)
    {
        if (path.StartsWith("/api/", StringComparison.OrdinalIgnoreCase))
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            await context.Response.WriteAsJsonAsync(new
            {
                error = "License upgrade required",
                requiredTier = requiredTier.ToString(),
                currentTier = currentTier.ToString()
            }, context.RequestAborted);
            return;
        }

        context.Response.Redirect("/");
    }

    private static LicenseTier GetRequiredTier(string path)
    {
        if (MatchesAnyPrefix(path, Tier2Prefixes))
            return LicenseTier.Professional;

        if (MatchesAnyPrefix(path, Tier1Prefixes))
            return LicenseTier.Starter;

        return LicenseTier.Free;
    }

    private static bool MatchesAnyPrefix(string path, string[] prefixes)
    {
        foreach (var prefix in prefixes)
        {
            if (path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }
}
