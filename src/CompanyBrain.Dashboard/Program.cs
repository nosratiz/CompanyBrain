using CompanyBrain.Dashboard;
using CompanyBrain.Dashboard.Api;
using CompanyBrain.Dashboard.Api.Serialization;
using CompanyBrain.Dashboard.DependencyInjection;
using CompanyBrain.Dashboard.Features.Auth.Interfaces;
using CompanyBrain.Dashboard.Features.Auth.Services;
using CompanyBrain.Dashboard.Mcp;
using CompanyBrain.Dashboard.Mcp.Resources;
using CompanyBrain.Dashboard.Mcp.Tools;
using CompanyBrain.Dashboard.Services;
using Microsoft.AspNetCore.Components.Authorization;
using MudBlazor.Services;

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

// Add Blazor Server services
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddMudServices();

// Authentication & Authorization (Blazor Server circuit-scoped)
builder.Services.AddCascadingAuthenticationState();
builder.Services.AddScoped<AuthTokenStore>();
builder.Services.AddScoped<TokenAuthenticationStateProvider>();
builder.Services.AddScoped<AuthenticationStateProvider>(sp =>
    sp.GetRequiredService<TokenAuthenticationStateProvider>());
builder.Services.AddAuthorizationCore();

// Add Company Brain API services
builder.Services.AddCompanyBrain(builder.Environment.ContentRootPath);
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.TypeInfoResolverChain.Insert(0, CompanyBrainJsonSerializerContext.Default);
});

// Add OpenAPI/Swagger
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new()
    {
        Title = "Company Brain API",
        Version = "v1",
        Description = "HTTP API for ingesting internal knowledge, browsing stored Markdown resources, and searching the company knowledge base. Also serves as an MCP server.",
    });
});

// Configure CORS to allow all origins
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

// Configure MCP Server
builder.Services.AddSingleton<McpSessionTracker>();
builder.Services
    .AddMcpServer()
    .WithHttpTransport()
    .WithTools<CompanyBrainTools>()
    .WithListResourcesHandler(KnowledgeResourceHandlers.ListResourcesAsync)
    .WithReadResourceHandler(KnowledgeResourceHandlers.ReadResourceAsync);

// Register HttpClient and KnowledgeApiClient for Blazor pages
// For server-side Blazor, we use a base address that will be configured at runtime
builder.Services.AddHttpClient<KnowledgeApiClient>((sp, client) =>
{
    // Use a localhost URL with the app's port (configured via launchSettings)
    var env = sp.GetRequiredService<IWebHostEnvironment>();
    if (env.IsDevelopment())
    {
        client.BaseAddress = new Uri("http://localhost:5200");
    }
    else
    {
        // In production, use HTTPS with the configured host
        client.BaseAddress = new Uri("http://localhost:8080");
    }
});

builder.Services.AddHttpClient<McpStatusClient>((sp, client) =>
{
    var env = sp.GetRequiredService<IWebHostEnvironment>();
    client.BaseAddress = env.IsDevelopment()
        ? new Uri("http://localhost:5200")
        : new Uri("http://localhost:8080");
});

// Auth API client – points at the CompanyBrainUserPanel API
builder.Services.AddHttpClient<AuthApiClient>((sp, client) =>
{
    var baseUrl = builder.Configuration["AuthApi:BaseUrl"] ?? "http://localhost:5000";
    client.BaseAddress = new Uri(baseUrl);
});
builder.Services.AddScoped<IAuthApiClient>(sp => sp.GetRequiredService<AuthApiClient>());

var app = builder.Build();

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}
else
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseCors();
app.UseSwagger();
app.UseSwaggerUI();

app.UseStaticFiles();
app.UseAntiforgery();

// Map Blazor components and API endpoints
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.MapGet("/dashboard", () => Results.Redirect("/"));
app.MapCompanyBrainApi();
app.MapMcp(mcpRoutePattern);

app.Run();

