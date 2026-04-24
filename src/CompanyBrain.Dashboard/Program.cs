using CompanyBrain.Dashboard;
using CompanyBrain.Dashboard.Api;
using CompanyBrain.Dashboard.DependencyInjection;
using CompanyBrain.Dashboard.Features.DocumentTenant;
using CompanyBrain.Dashboard.Features.AutoSetup.Api;
using CompanyBrain.Dashboard.Features.ChatRelay.Api;
using CompanyBrain.Dashboard.Features.Confluence.DependencyInjection;
using CompanyBrain.Dashboard.Features.SharePoint.DependencyInjection;

const string mcpRoutePattern = "/mcp";

if (args.Contains("--stdio"))
{
    await RunMcpStdioAsync(args);
    return;
}

var builder = WebApplication.CreateBuilder(args);

// Configure logging
builder.Logging.ClearProviders();
builder.Logging.AddSimpleConsole(options =>
{
    options.TimestampFormat = "yyyy-MM-dd HH:mm:ss ";
    options.IncludeScopes = true;
    options.SingleLine = true;
});

// Add all Dashboard services
builder.Services.AddDashboardServices(builder.Configuration, builder.Environment);

var app = builder.Build();

// Initialize databases
await app.InitializeDatabaseAsync();
await app.InitializeAuditDatabaseAsync();
await app.Services.InitializeSharePointDatabaseAsync();
await app.Services.InitializeConfluenceDatabaseAsync();

// Configure middleware pipeline
app.UseDashboardMiddleware();

// Map Blazor components and API endpoints
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

app.Run();

// --stdio mode: minimal host that speaks MCP over stdin/stdout
static async Task RunMcpStdioAsync(string[] args)
{
    var builder = Host.CreateApplicationBuilder(args);

    // stdout is reserved for the MCP protocol — silence all logging
    builder.Logging.ClearProviders();

    builder.Services
        .AddDashboardCompanyBrain(builder.Environment.ContentRootPath)
        .AddDashboardDatabase(builder.Configuration)
        .AddDashboardAudit(builder.Configuration)
        .AddSharePointMirror(builder.Configuration)
        .AddDashboardScripting()
        .AddDashboardMcpStdio(builder.Configuration);

    var host = builder.Build();

    await host.Services.InitializeMcpDatabasesAsync();
    await host.Services.InitializeSharePointDatabaseAsync();

    await host.RunAsync();
}
