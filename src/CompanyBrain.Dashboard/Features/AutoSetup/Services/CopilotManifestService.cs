using System.Text.Json;
using System.Text.Json.Serialization;

namespace CompanyBrain.Dashboard.Features.AutoSetup.Services;

/// <summary>
/// Generates a Microsoft Copilot / Teams Agent manifest for CompanyBrain.
/// Creates a local manifest.json that registers CompanyBrain as a Copilot Agent
/// capable of answering questions from the local knowledge base.
/// </summary>
public sealed class CopilotManifestService(
    ILogger<CopilotManifestService> logger,
    IHostEnvironment environment)
{
    private const string ManifestFileName = "copilot-agent-manifest.json";

    public sealed record ManifestResult(
        bool Success,
        string Message,
        string? ManifestPath = null);

    /// <summary>
    /// Generates the Copilot Agent manifest in the app's content root.
    /// </summary>
    public async Task<ManifestResult> GenerateManifestAsync(
        string? baseUrl = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var effectiveUrl = baseUrl ?? (environment.IsDevelopment()
                ? "http://localhost:5202"
                : "http://localhost:8080");

            var manifest = BuildManifest(effectiveUrl);

            var json = JsonSerializer.Serialize(manifest, ManifestSerializerOptions);

            // Validate JSON before writing
            JsonDocument.Parse(json).Dispose();

            var manifestPath = Path.Combine(environment.ContentRootPath, ManifestFileName);
            await File.WriteAllTextAsync(manifestPath, json, cancellationToken);

            logger.LogInformation("Generated Copilot Agent manifest at {Path}", manifestPath);
            return new ManifestResult(true, $"Manifest generated at {manifestPath}", manifestPath);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to generate Copilot manifest");
            return new ManifestResult(false, $"Failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Checks if the manifest already exists.
    /// </summary>
    public bool ManifestExists()
    {
        var path = Path.Combine(environment.ContentRootPath, ManifestFileName);
        return File.Exists(path);
    }

    /// <summary>
    /// Returns the manifest path if it exists.
    /// </summary>
    public string? GetManifestPath()
    {
        var path = Path.Combine(environment.ContentRootPath, ManifestFileName);
        return File.Exists(path) ? path : null;
    }

    private static CopilotManifest BuildManifest(string baseUrl)
    {
        return new CopilotManifest
        {
            Schema = "https://developer.microsoft.com/json-schemas/copilot/plugin/v2.2/schema.json",
            SchemaVersion = "v2.2",
            NameForHuman = "CompanyBrain",
            DescriptionForHuman = "Search and retrieve internal company knowledge, SharePoint documents, and wiki content.",
            DescriptionForModel = "Use this plugin to search the company's internal knowledge base. " +
                                  "It contains synced SharePoint documents, wiki pages, internal policies, " +
                                  "and technical documentation. Query it when the user asks about company-specific " +
                                  "information, internal processes, or organizational knowledge.",
            LogoUrl = $"{baseUrl}/favicon.ico",
            ContactEmail = "admin@companybrain.local",
            Namespace = "CompanyBrain",
            Capabilities = new ManifestCapabilities
            {
                ConversationStarters =
                [
                    new ConversationStarter { Text = "Search our knowledge base for onboarding procedures" },
                    new ConversationStarter { Text = "What SharePoint documents do we have about project guidelines?" },
                    new ConversationStarter { Text = "Find internal policies about remote work" }
                ]
            },
            Runtimes =
            [
                new ManifestRuntime
                {
                    Type = "OpenApi",
                    Auth = new RuntimeAuth
                    {
                        Type = "None"
                    },
                    Spec = new RuntimeSpec
                    {
                        Url = $"{baseUrl}/swagger/v1/swagger.json"
                    },
                    RunForFunctions = ["searchDocs", "listResources", "searchSharePoint"]
                }
            ],
            Functions =
            [
                new ManifestFunction
                {
                    Name = "searchDocs",
                    Description = "Search the company knowledge base using full-text search.",
                    Parameters = new FunctionParameters
                    {
                        Type = "object",
                        Properties = new Dictionary<string, FunctionProperty>
                        {
                            ["query"] = new() { Type = "string", Description = "The search query" },
                            ["maxResults"] = new() { Type = "integer", Description = "Maximum results to return (default 10)" }
                        },
                        Required = ["query"]
                    }
                },
                new ManifestFunction
                {
                    Name = "listResources",
                    Description = "List all available knowledge resources and documents.",
                    Parameters = new FunctionParameters
                    {
                        Type = "object",
                        Properties = new Dictionary<string, FunctionProperty>()
                    }
                },
                new ManifestFunction
                {
                    Name = "searchSharePoint",
                    Description = "Search locally mirrored SharePoint content.",
                    Parameters = new FunctionParameters
                    {
                        Type = "object",
                        Properties = new Dictionary<string, FunctionProperty>
                        {
                            ["query"] = new() { Type = "string", Description = "The search query" },
                            ["maxResults"] = new() { Type = "integer", Description = "Maximum results (default 10)" }
                        },
                        Required = ["query"]
                    }
                }
            ]
        };
    }

    private static readonly JsonSerializerOptions ManifestSerializerOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    #region Manifest Model

    private sealed class CopilotManifest
    {
        [JsonPropertyName("$schema")]
        public string Schema { get; set; } = "";
        public string SchemaVersion { get; set; } = "";
        public string NameForHuman { get; set; } = "";
        public string DescriptionForHuman { get; set; } = "";
        public string DescriptionForModel { get; set; } = "";
        public string LogoUrl { get; set; } = "";
        public string ContactEmail { get; set; } = "";
        public string Namespace { get; set; } = "";
        public ManifestCapabilities? Capabilities { get; set; }
        public ManifestRuntime[] Runtimes { get; set; } = [];
        public ManifestFunction[] Functions { get; set; } = [];
    }

    private sealed class ManifestCapabilities
    {
        public ConversationStarter[] ConversationStarters { get; set; } = [];
    }

    private sealed class ConversationStarter
    {
        public string Text { get; set; } = "";
    }

    private sealed class ManifestRuntime
    {
        public string Type { get; set; } = "";
        public RuntimeAuth? Auth { get; set; }
        public RuntimeSpec? Spec { get; set; }
        public string[] RunForFunctions { get; set; } = [];
    }

    private sealed class RuntimeAuth
    {
        public string Type { get; set; } = "None";
    }

    private sealed class RuntimeSpec
    {
        public string Url { get; set; } = "";
    }

    private sealed class ManifestFunction
    {
        public string Name { get; set; } = "";
        public string Description { get; set; } = "";
        public FunctionParameters? Parameters { get; set; }
    }

    private sealed class FunctionParameters
    {
        public string Type { get; set; } = "object";
        public Dictionary<string, FunctionProperty> Properties { get; set; } = new();
        public string[]? Required { get; set; }
    }

    private sealed class FunctionProperty
    {
        public string Type { get; set; } = "";
        public string Description { get; set; } = "";
    }

    #endregion
}
