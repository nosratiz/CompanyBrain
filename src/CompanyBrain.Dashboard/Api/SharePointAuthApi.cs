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

        // Step 2: Handle the callback from Microsoft with the auth code
        app.MapGet("/api/sharepoint/callback", async (
            string code,
            SharePointOAuthService oAuthService,
            HttpRequest request,
            CancellationToken ct) =>
        {
            var redirectUri = $"{request.Scheme}://{request.Host}/api/sharepoint/callback";
            await oAuthService.AcquireTokenByAuthCodeAsync(code, redirectUri, ct);
            return Results.Redirect("/sharepoint");
        });

        return app;
    }
}
