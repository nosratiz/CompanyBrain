using CompanyBrain.Dashboard;
using CompanyBrain.Dashboard.Api;
using CompanyBrain.Dashboard.DependencyInjection;
using CompanyBrain.Dashboard.Features.DocumentTenant;
using CompanyBrain.Dashboard.Features.Confluence.DependencyInjection;
using CompanyBrain.Dashboard.Features.SharePoint.DependencyInjection;

const string mcpRoutePattern = "/mcp";

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
app.MapMcp(mcpRoutePattern);

app.Run();

