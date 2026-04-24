namespace CompanyBrain.Dashboard.DependencyInjection;

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

    public string Title { get; set; } = "Deep Root API";
    public string Version { get; set; } = "v1";
    public string Description { get; set; } = "HTTP API for ingesting internal knowledge, browsing stored Markdown resources, and searching the company knowledge base. Also serves as an MCP server.";
}
