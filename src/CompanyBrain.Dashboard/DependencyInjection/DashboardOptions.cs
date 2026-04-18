namespace CompanyBrain.Dashboard.DependencyInjection;

/// <summary>
/// Configuration options for the Dashboard application.
/// </summary>
public sealed class DashboardOptions
{
    public const string SectionName = "Dashboard";

    /// <summary>
    /// Base URL for the application in development.
    /// </summary>
    public string DevelopmentBaseUrl { get; set; } = "http://localhost:5202";

    /// <summary>
    /// Base URL for the application in production.
    /// </summary>
    public string ProductionBaseUrl { get; set; } = "http://localhost:8080";
}

/// <summary>
/// Configuration options for external API clients.
/// </summary>
public sealed class ExternalApiOptions
{
    public const string SectionName = "ExternalApis";

    /// <summary>
    /// Base URL for the Auth API.
    /// </summary>
    public string AuthApiBaseUrl { get; set; } = "http://localhost:5000";

    /// <summary>
    /// Base URL for the Tenant API.
    /// </summary>
    public string TenantApiBaseUrl { get; set; } = "http://localhost:5000";
}

/// <summary>
/// Configuration options for Swagger/OpenAPI.
/// </summary>
public sealed class SwaggerOptions
{
    public const string SectionName = "Swagger";

    public string Title { get; set; } = "Company Brain API";
    public string Version { get; set; } = "v1";
    public string Description { get; set; } = "HTTP API for ingesting internal knowledge, browsing stored Markdown resources, and searching the company knowledge base. Also serves as an MCP server.";
}
