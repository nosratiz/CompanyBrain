namespace CompanyBrain.Landing.Services;

public sealed class TenantConfiguration
{
    public string TenantSlug { get; set; } = "default";
    public string CompanyName { get; set; } = "CompanyBrain";
    public string Tagline { get; set; } = "Your AI-Powered Internal Knowledge Engine";
    public string Description { get; set; } = "Transform scattered company knowledge into instant, intelligent answers. Built for teams that move fast.";
    public string PrimaryColor { get; set; } = "#667eea";
    public string SecondaryColor { get; set; } = "#764ba2";
    public string AccentColor { get; set; } = "#f093fb";
    public string? LogoUrl { get; set; }
    public List<FeatureItem> Features { get; set; } = DefaultFeatures();
    public string RegistrationEndpoint { get; set; } = "http://localhost:5130/api/auth/register";

    public static List<FeatureItem> DefaultFeatures() =>
    [
        new("Bolt", "AI Knowledge Search", "Ask questions in plain language. Get precise answers sourced from your docs, wikis, and internal tools — in seconds.", true),
        new("Security", "Enterprise-Grade Security", "End-to-end encryption, per-tenant isolation, and SOC 2 compliance. Your data never leaves your boundary."),
        new("Speed", "Blazing Fast Indexing", "Index thousands of documents in minutes. Our adaptive pipeline handles PDFs, Markdown, HTML, and more."),
        new("Hub", "Multi-Tenant by Design", "Each team gets their own isolated knowledge silo. Share across boundaries only when you choose to."),
        new("Analytics", "Usage Analytics", "See what your team searches for most. Identify knowledge gaps and measure content impact."),
        new("Api", "Developer-First API", "RESTful endpoints, API key management, and MCP protocol support. Integrate with any workflow.")
    ];
}

public sealed record FeatureItem(string Icon, string Title, string Description, bool IsHighlighted = false);
