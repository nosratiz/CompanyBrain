using CompanyBrain.Dashboard.Api.Serialization;
using CompanyBrain.Dashboard.Data;
using CompanyBrain.Dashboard.Features.Auth.Interfaces;
using CompanyBrain.Dashboard.Features.Auth.Services;
using CompanyBrain.Dashboard.Features.DocumentTenant.Validators;
using CompanyBrain.Dashboard.Features.SharePoint.DependencyInjection;
using CompanyBrain.Dashboard.Mcp;
using CompanyBrain.Dashboard.Mcp.Resources;
using CompanyBrain.Dashboard.Mcp.Tools;
using CompanyBrain.Dashboard.Middleware;
using CompanyBrain.Dashboard.Scripting;
using CompanyBrain.Dashboard.Services;
using FluentValidation;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.EntityFrameworkCore;
using MudBlazor.Services;

namespace CompanyBrain.Dashboard.DependencyInjection;

/// <summary>
/// Extension methods for configuring Dashboard services.
/// </summary>
public static class DashboardServiceCollectionExtensions
{
    /// <summary>
    /// Adds all Dashboard services to the service collection.
    /// </summary>
    public static IServiceCollection AddDashboardServices(
        this IServiceCollection services,
        IConfiguration configuration,
        IWebHostEnvironment environment)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(environment);

        services
            .AddDashboardBlazor()
            .AddDashboardAuthentication(configuration)
            .AddDashboardCompanyBrain(environment.ContentRootPath)
            .AddDashboardSwagger(configuration)
            .AddDashboardCors()
            .AddDashboardMcp()
            .AddDashboardScripting()
            .AddDashboardHttpClients(configuration, environment)
            .AddDashboardDatabase(configuration)
            .AddDashboardValidation()
            .AddSharePointMirror(configuration);

        return services;
    }

    /// <summary>
    /// Adds Blazor Server and MudBlazor services.
    /// </summary>
    public static IServiceCollection AddDashboardBlazor(this IServiceCollection services)
    {
        services.AddRazorComponents()
            .AddInteractiveServerComponents();

        services.AddMudServices();
        
        // Network status monitoring
        services.AddScoped<NetworkStatusService>();

        return services;
    }

    /// <summary>
    /// Adds authentication and authorization services.
    /// </summary>
    public static IServiceCollection AddDashboardAuthentication(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddCascadingAuthenticationState();
        services.AddScoped<IAuthSessionStorage, BrowserAuthSessionStorage>();
        services.AddScoped<BrowserAuthSessionStorage>(sp =>
            (BrowserAuthSessionStorage)sp.GetRequiredService<IAuthSessionStorage>());
        services.AddScoped<AuthTokenStore>();
        services.AddScoped<TokenAuthenticationStateProvider>();
        services.AddScoped<AuthenticationStateProvider>(sp =>
            sp.GetRequiredService<TokenAuthenticationStateProvider>());

        services.AddAuthentication();
        services.AddAuthorization();
        services.AddAuthorizationCore();

        return services;
    }

    /// <summary>
    /// Adds Company Brain core services.
    /// </summary>
    public static IServiceCollection AddDashboardCompanyBrain(this IServiceCollection services, string contentRootPath)
    {
        services.AddCompanyBrain(contentRootPath);
        services.ConfigureHttpJsonOptions(options =>
        {
            options.SerializerOptions.TypeInfoResolverChain.Insert(0, CompanyBrainJsonSerializerContext.Default);
        });

        return services;
    }

    /// <summary>
    /// Adds Swagger/OpenAPI services.
    /// </summary>
    public static IServiceCollection AddDashboardSwagger(this IServiceCollection services, IConfiguration configuration)
    {
        var swaggerOptions = configuration.GetSection(SwaggerOptions.SectionName).Get<SwaggerOptions>()
            ?? new SwaggerOptions();

        services.AddEndpointsApiExplorer();
        services.AddSwaggerGen(options =>
        {
            options.SwaggerDoc(swaggerOptions.Version, new()
            {
                Title = swaggerOptions.Title,
                Version = swaggerOptions.Version,
                Description = swaggerOptions.Description,
            });
        });

        return services;
    }

    /// <summary>
    /// Adds CORS configuration.
    /// </summary>
    public static IServiceCollection AddDashboardCors(this IServiceCollection services)
    {
        services.AddCors(options =>
        {
            options.AddDefaultPolicy(policy =>
            {
                policy.AllowAnyOrigin()
                      .AllowAnyMethod()
                      .AllowAnyHeader();
            });
        });

        return services;
    }

    /// <summary>
    /// Adds MCP Server services with dynamic tool support and governance filtering.
    /// </summary>
    public static IServiceCollection AddDashboardMcp(this IServiceCollection services)
    {
        services.AddSingleton<McpSessionTracker>();
        services.AddSingleton<McpGovernanceFilter>();
        services.AddSingleton<GovernanceToolWrapper>();
        services
            .AddMcpServer()
            .WithHttpTransport()
            .WithTools<CompanyBrainTools>()
            .WithTools<ResourceTemplateTools>()
            .WithSharePointTools()
            .WithDynamicTools()
            .WithListResourcesHandler(CompositeResourceHandlers.ListResourcesAsync)
            .WithReadResourceHandler(CompositeResourceHandlers.ReadResourceAsync);

        return services;
    }
    
    /// <summary>
    /// Adds scripting services for the Dynamic MCP Tool Builder.
    /// </summary>
    public static IServiceCollection AddDashboardScripting(this IServiceCollection services)
    {
        services.AddScoped<ScriptRunnerService>();
        services.AddScoped<CustomToolService>();
        
        return services;
    }

    /// <summary>
    /// Adds HTTP client services for external APIs.
    /// </summary>
    public static IServiceCollection AddDashboardHttpClients(
        this IServiceCollection services,
        IConfiguration configuration,
        IWebHostEnvironment environment)
    {
        var dashboardOptions = configuration.GetSection(DashboardOptions.SectionName).Get<DashboardOptions>()
            ?? new DashboardOptions();
        var externalApiOptions = configuration.GetSection(ExternalApiOptions.SectionName).Get<ExternalApiOptions>()
            ?? new ExternalApiOptions();

        // Register the 401 redirect handler
        services.AddTransient<UnauthorizedRedirectHandler>();

        // Knowledge API client for Blazor pages
        services.AddHttpClient<KnowledgeApiClient>((sp, client) =>
        {
            var env = sp.GetRequiredService<IWebHostEnvironment>();
            client.BaseAddress = new Uri(env.IsDevelopment()
                ? dashboardOptions.DevelopmentBaseUrl
                : dashboardOptions.ProductionBaseUrl);
        })
        .AddHttpMessageHandler<UnauthorizedRedirectHandler>();

        // MCP Status client
        services.AddHttpClient<McpStatusClient>((sp, client) =>
        {
            var env = sp.GetRequiredService<IWebHostEnvironment>();
            client.BaseAddress = new Uri(env.IsDevelopment()
                ? dashboardOptions.DevelopmentBaseUrl
                : dashboardOptions.ProductionBaseUrl);
        })
        .AddHttpMessageHandler<UnauthorizedRedirectHandler>();

        // Document-Tenant API client (internal API)
        services.AddHttpClient<DocumentTenantApiClient>((sp, client) =>
        {
            var env = sp.GetRequiredService<IWebHostEnvironment>();
            client.BaseAddress = new Uri(env.IsDevelopment()
                ? dashboardOptions.DevelopmentBaseUrl
                : dashboardOptions.ProductionBaseUrl);
        })
        .AddHttpMessageHandler<UnauthorizedRedirectHandler>();

        // Auth API client (no redirect handler - this handles login itself)
        services.AddHttpClient<AuthApiClient>((_, client) =>
        {
            client.BaseAddress = new Uri(externalApiOptions.AuthApiBaseUrl);
        });
        services.AddScoped<IAuthApiClient>(sp => sp.GetRequiredService<AuthApiClient>());

        // External Tenant API client
        services.AddHttpClient<ExternalTenantApiClient>((_, client) =>
        {
            client.BaseAddress = new Uri(externalApiOptions.TenantApiBaseUrl);
        })
        .AddHttpMessageHandler<UnauthorizedRedirectHandler>();

        return services;
    }

    /// <summary>
    /// Adds SQLite database context for document-tenant assignments.
    /// </summary>
    public static IServiceCollection AddDashboardDatabase(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DocumentAssignments")
            ?? "Data Source=document_assignments.db";

        // Use AddDbContextFactory with ServiceLifetime.Singleton for IDbContextFactory<T>
        // - Factory is singleton for thread-safe access from singletons (SettingsService, MCP handlers)
        // - DbContext instances created by the factory are still short-lived and disposed properly
        services.AddDbContextFactory<DocumentAssignmentDbContext>(
            options => options.UseSqlite(connectionString),
            ServiceLifetime.Singleton);
        
        // Register SettingsService as singleton for cross-request caching
        services.AddSingleton<SettingsService>();

        return services;
    }

    /// <summary>
    /// Adds FluentValidation validators.
    /// </summary>
    public static IServiceCollection AddDashboardValidation(this IServiceCollection services)
    {
        services.AddValidatorsFromAssemblyContaining<AssignDocumentToTenantRequestValidator>();

        return services;
    }
}
