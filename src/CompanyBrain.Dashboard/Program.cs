using CompanyBrain.Dashboard;
using CompanyBrain.Dashboard.Api;
using CompanyBrain.Dashboard.DependencyInjection;
using CompanyBrain.Dashboard.Features.DocumentTenant;

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

// Initialize database
await app.InitializeDatabaseAsync();

// Configure middleware pipeline
app.UseDashboardMiddleware();

// Map Blazor components and API endpoints
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.MapGet("/dashboard", () => Results.Redirect("/"));
app.MapCompanyBrainApi();
app.MapDocumentTenantApi();
app.MapMcp(mcpRoutePattern);

app.Run();

