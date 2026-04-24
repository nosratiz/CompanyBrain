using CompanyBrain.Dashboard.Api;
using CompanyBrain.Dashboard.Features.AutoSetup.Api;
using CompanyBrain.Dashboard.Features.ChatRelay.Api;
using CompanyBrain.Dashboard.Features.Confluence.DependencyInjection;
using CompanyBrain.Dashboard.Features.DocumentTenant;
using CompanyBrain.Dashboard.Features.SharePoint.DependencyInjection;
using CompanyBrain.Dashboard.Services;
using CompanyBrain.Pruning;
using CompanyBrain.Search.Vector;

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

        // Create the embedding_cache table in the vector DB before the
        // first GenerateAsync call tries to read/write it.
        await app.Services.GetRequiredService<EmbeddingCache>()
                          .EnsureSchemaAsync(CancellationToken.None);

        // Sync pruning engine configuration from the database so that
        // in-memory defaults are replaced with user-saved values.
        var savedSettings = await app.Services
            .GetRequiredService<SettingsService>()
            .GetSettingsAsync(CancellationToken.None);

        var pruningConfig = app.Services.GetRequiredService<PruningConfiguration>();
        pruningConfig.Enabled              = savedSettings.PruningEnabled;
        pruningConfig.RelevanceThreshold   = savedSettings.PruningRelevanceThreshold;
        pruningConfig.MaxChunks            = savedSettings.PruningMaxChunks;
        pruningConfig.TokenBudget          = savedSettings.PruningTokenBudget;
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
