using CompanyBrain.Dashboard.Api;
using CompanyBrain.Dashboard.Features.AutoSetup.Api;
using CompanyBrain.Dashboard.Features.ChatRelay.Api;
using CompanyBrain.Dashboard.Features.Confluence.DependencyInjection;
using CompanyBrain.Dashboard.Features.DocumentTenant;
using CompanyBrain.Dashboard.Features.SharePoint.DependencyInjection;

namespace CompanyBrain.Dashboard.DependencyInjection;

/// <summary>
/// Public bootstrap helpers that let an external host (e.g. the
/// DeepRoot.Photino desktop shell) wire up exactly the same endpoints
/// and database initialisation as the Dashboard's own <c>Program.cs</c>.
/// </summary>
public static class DashboardEndpointRouteBuilderExtensions
{
    public const string DefaultMcpRoutePattern = "/mcp";

    /// <summary>
    /// Runs every database <c>EnsureCreated</c> + schema patch the
    /// Dashboard expects on startup.
    /// </summary>
    public static async Task InitializeDashboardDatabasesAsync(this WebApplication app)
    {
        ArgumentNullException.ThrowIfNull(app);

        await app.InitializeDatabaseAsync();
        await app.InitializeAuditDatabaseAsync();
        await app.Services.InitializeSharePointDatabaseAsync();
        await app.Services.InitializeConfluenceDatabaseAsync();
    }

    /// <summary>
    /// Maps the full set of Dashboard HTTP endpoints — Razor components,
    /// REST APIs, webhooks, and the MCP transport.
    /// </summary>
    public static WebApplication MapDashboardEndpoints(
        this WebApplication app,
        string mcpRoutePattern = DefaultMcpRoutePattern)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapRazorComponents<App>()
           .AddInteractiveServerRenderMode();

        app.MapGet("/dashboard", () => Results.Redirect("/"));
        app.MapCompanyBrainApi();
        app.MapResourceTemplateApi();
        app.MapDocumentTenantApi();
        app.MapSharePointAuthApi();
        app.MapAutoSetupApi();
        app.MapDeepRootSettingsApi();
        app.MapSlackWebhook();
        app.MapTeamsWebhook();
        app.MapChatRelaySettingsApi();
        app.MapMcp(mcpRoutePattern);

        return app;
    }
}
