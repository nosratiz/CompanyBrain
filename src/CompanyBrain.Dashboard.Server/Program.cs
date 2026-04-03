using CompanyBrain.MultiTenant.Api;
using CompanyBrain.MultiTenant.DependencyInjection;
using CompanyBrain.MultiTenant.Middleware;
using Microsoft.AspNetCore.Http.Json;
using System.Text.Json.Serialization;

var builder = WebApplication.CreateBuilder(args);

// Configure logging
builder.Logging.ClearProviders();
builder.Logging.AddSimpleConsole(options =>
{
    options.TimestampFormat = "yyyy-MM-dd HH:mm:ss ";
    options.IncludeScopes = true;
    options.SingleLine = true;
});

// Get configuration
var connectionString = builder.Configuration.GetConnectionString("TenantDb")
    ?? "Data Source=tenants.db";
var storagePath = builder.Configuration["Storage:BasePath"]
    ?? Path.Combine(builder.Environment.ContentRootPath, "tenant-data");
var mcpServerUrl = builder.Configuration["Mcp:ServerUrl"]
    ?? "http://localhost:5003";

// Add multi-tenant services
builder.Services.AddCompanyBrainMultiTenant(connectionString, storagePath);

// Configure JSON serialization for enums
builder.Services.Configure<JsonOptions>(options =>
{
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
});

// Add Swagger
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new()
    {
        Title = "Company Brain Multi-Tenant API",
        Version = "v1",
        Description = "API for managing tenants, API keys, and multi-tenant knowledge stores.",
    });
});

// Add CORS for local development
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

var app = builder.Build();

// Ensure database exists
await app.Services.EnsureTenantDatabaseAsync();

// Development middleware
if (app.Environment.IsDevelopment())
{
    app.UseWebAssemblyDebugging();
}

app.UseSwagger();
app.UseSwaggerUI();

app.UseCors();

// Serve Blazor WebAssembly files
app.UseBlazorFrameworkFiles();
app.UseStaticFiles();

// API key authentication middleware (for /api routes)
app.UseWhen(
    context => context.Request.Path.StartsWithSegments("/api"),
    appBuilder => appBuilder.UseMiddleware<ApiKeyAuthenticationMiddleware>());

// Map tenant API endpoints
app.MapTenantApi(mcpServerUrl);

// Fallback to index.html for Blazor routing
app.MapFallbackToFile("index.html");

app.Run();
