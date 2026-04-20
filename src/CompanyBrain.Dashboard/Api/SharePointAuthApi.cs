using CompanyBrain.Dashboard.Features.SharePoint.Services;

namespace CompanyBrain.Dashboard.Api;

/// <summary>
/// Minimal API endpoints for the SharePoint OAuth2 authorization code flow.
/// This is completely separate from the application's own authentication.
/// </summary>
public static class SharePointAuthApi
{
    public static WebApplication MapSharePointAuthApi(this WebApplication app)
    {
        // Step 1: Redirect user to Microsoft login to grant SharePoint access
        app.MapGet("/api/sharepoint/connect", async (
            SharePointOAuthService oAuthService,
            HttpRequest request,
            CancellationToken ct) =>
        {
            var redirectUri = $"{request.Scheme}://{request.Host}/api/sharepoint/callback";
            var authUrl = await oAuthService.GetAuthorizationUrlAsync(redirectUri, ct);
            return Results.Redirect(authUrl.ToString());
        });

        // Step 2: Handle the callback from Microsoft with the auth code OR admin consent
        app.MapGet("/api/sharepoint/callback", async (
            SharePointOAuthService oAuthService,
            HttpRequest request,
            CancellationToken ct) =>
        {
            if (request.Query.ContainsKey("admin_consent"))
                return HandleAdminConsentCallback(request);

            if (!request.Query.ContainsKey("code"))
                return Results.Redirect("/sharepoint?error=missing_code");

            var code = request.Query["code"].ToString();
            var redirectUri = $"{request.Scheme}://{request.Host}/api/sharepoint/callback";
            await oAuthService.AcquireTokenByAuthCodeAsync(code, redirectUri, ct);
            return Results.Redirect("/sharepoint");
        });

        // Step 3: Redirect to Microsoft admin consent prompt
        app.MapGet("/api/sharepoint/admin-consent", async (
            SharePointSettingsProvider settingsProvider,
            HttpRequest request,
            CancellationToken ct) =>
        {
            var options = await settingsProvider.GetEffectiveOptionsAsync(ct);
            var redirectUri = $"{request.Scheme}://{request.Host}/api/sharepoint/callback";

            // The redirect_uri MUST be registered in Azure Portal → App registrations →
            // Authentication → Web → Redirect URIs. Add:
            //   http://localhost:5202/api/sharepoint/callback
            var consentUrl = $"https://login.microsoftonline.com/{options.TenantId}/adminconsent" +
                             $"?client_id={Uri.EscapeDataString(options.ClientId)}" +
                             $"&redirect_uri={Uri.EscapeDataString(redirectUri)}" +
                             $"&scope=https://graph.microsoft.com/.default";
            return Results.Redirect(consentUrl);
        });

        return app;
    }

    private static IResult HandleAdminConsentCallback(HttpRequest request)
    {
        var consented = string.Equals(
            request.Query["admin_consent"], "True", StringComparison.OrdinalIgnoreCase);

        return consented
            ? Results.Redirect("/sharepoint?admin_consent=granted")
            : Results.Redirect("/sharepoint?admin_consent=declined");
    }
}
