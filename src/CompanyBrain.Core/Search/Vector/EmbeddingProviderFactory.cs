using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace CompanyBrain.Search.Vector;

/// <summary>
/// Runtime owner of the configured <see cref="IEmbeddingGenerator{String, Embedding}"/> and the
/// <see cref="SqliteVecStore"/> that matches its dimensions. Re-reads
/// <see cref="IEmbeddingOptionsAccessor"/> on every access and rebuilds when the signature changes,
/// so operators can update embedding settings at runtime (e.g. via the Settings UI) without a
/// process restart.
/// </summary>
public sealed class EmbeddingProviderFactory : IDisposable
{
    private readonly IEmbeddingOptionsAccessor accessor;
    private readonly IHttpClientFactory httpClientFactory;
    private readonly EmbeddingCache cache;
    private readonly ILoggerFactory loggerFactory;
    private readonly Lock gate = new();

    private EmbeddingOptionsSignature cachedSignature;
    private IEmbeddingGenerator<string, Embedding<float>>? cachedGenerator;
    private SqliteVecStore? cachedStore;
    private string defaultDatabasePath = string.Empty;

    public EmbeddingProviderFactory(
        IEmbeddingOptionsAccessor accessor,
        IHttpClientFactory httpClientFactory,
        EmbeddingCache cache,
        ILoggerFactory loggerFactory)
    {
        this.accessor = accessor;
        this.httpClientFactory = httpClientFactory;
        this.cache = cache;
        this.loggerFactory = loggerFactory;
    }

    public string ResolvedModel { get; private set; } = string.Empty;
    public int ResolvedDimensions { get; private set; }
    public EmbeddingProviderType ResolvedProvider { get; private set; } = EmbeddingProviderType.None;

    /// <summary>
    /// Database path used when <see cref="EmbeddingOptions.DatabasePath"/> is not supplied.
    /// Set by <c>AddDeepRootVectorSearch</c> at startup.
    /// </summary>
    public string DefaultDatabasePath
    {
        get => defaultDatabasePath;
        set => defaultDatabasePath = value ?? string.Empty;
    }

    /// <summary>
    /// Returns the current embedding generator, or <c>null</c> when the configured provider is
    /// <see cref="EmbeddingProviderType.None"/>. Caches per options signature.
    /// </summary>
    public IEmbeddingGenerator<string, Embedding<float>>? GetGeneratorOrNull()
    {
        Refresh();
        return cachedGenerator;
    }

    /// <summary>
    /// Returns the current vector store, or <c>null</c> when no provider is configured or no
    /// database path has been resolved.
    /// </summary>
    public SqliteVecStore? GetStoreOrNull()
    {
        Refresh();
        return cachedStore;
    }

    /// <summary>
    /// Forces the next access to rebuild from a fresh accessor snapshot. Call after the Settings
    /// UI updates the database-backed configuration.
    /// </summary>
    public void Reload()
    {
        lock (gate)
        {
            cachedSignature = default;
            DisposeCachedGenerator();
            cachedGenerator = null;
            cachedStore = null;
        }
    }

    public void Dispose()
    {
        lock (gate)
        {
            DisposeCachedGenerator();
            cachedGenerator = null;
            cachedStore = null;
        }
    }

    private void Refresh()
    {
        var snapshot = accessor.GetCurrent();
        var signature = EmbeddingOptionsSignature.From(snapshot);

        lock (gate)
        {
            if (signature == cachedSignature)
            {
                return;
            }

            DisposeCachedGenerator();
            cachedGenerator = null;
            cachedStore = null;
            cachedSignature = signature;

            if (snapshot.Provider == EmbeddingProviderType.None)
            {
                ResolvedProvider = EmbeddingProviderType.None;
                ResolvedModel = string.Empty;
                ResolvedDimensions = 0;
                return;
            }

            var (model, dimensions) = EmbeddingProviderDefaults.Resolve(snapshot.Provider, snapshot.Model, snapshot.Dimensions);
            IEmbeddingGenerator<string, Embedding<float>> inner = snapshot.Provider switch
            {
                EmbeddingProviderType.OpenAI => CreateOpenAI(snapshot, model, dimensions),
                EmbeddingProviderType.Gemini => CreateGemini(snapshot, model, dimensions),
                EmbeddingProviderType.Voyage => CreateVoyage(snapshot, model, dimensions),
                _ => throw new InvalidOperationException($"Unsupported embedding provider: {snapshot.Provider}"),
            };

            cachedGenerator = new CachingEmbeddingGenerator(
                inner,
                cache,
                model,
                dimensions,
                loggerFactory.CreateLogger<CachingEmbeddingGenerator>());

            var dbPath = string.IsNullOrWhiteSpace(snapshot.DatabasePath) ? defaultDatabasePath : snapshot.DatabasePath;
            if (!string.IsNullOrWhiteSpace(dbPath))
            {
                cachedStore = new SqliteVecStore(dbPath, dimensions, loggerFactory.CreateLogger<SqliteVecStore>());
            }

            ResolvedProvider = snapshot.Provider;
            ResolvedModel = model;
            ResolvedDimensions = dimensions;
        }
    }

    private IEmbeddingGenerator<string, Embedding<float>> CreateOpenAI(EmbeddingOptions snapshot, string model, int dimensions)
    {
        var apiKey = snapshot.ApiKey
            ?? Environment.GetEnvironmentVariable("OPENAI_API_KEY")
            ?? throw new InvalidOperationException(
                "OpenAI embedding provider requires an ApiKey or OPENAI_API_KEY.");

        Uri? endpoint = string.IsNullOrWhiteSpace(snapshot.Endpoint) ? null : new Uri(snapshot.Endpoint);
        return new OpenAIEmbeddingGenerator(apiKey, model, dimensions, endpoint);
    }

    private IEmbeddingGenerator<string, Embedding<float>> CreateGemini(EmbeddingOptions snapshot, string model, int dimensions)
    {
        var apiKey = snapshot.ApiKey
            ?? Environment.GetEnvironmentVariable("GEMINI_API_KEY")
            ?? Environment.GetEnvironmentVariable("GOOGLE_API_KEY")
            ?? throw new InvalidOperationException(
                "Gemini embedding provider requires an ApiKey or GEMINI_API_KEY.");

        var http = httpClientFactory.CreateClient("DeepRootGemini");
        return new GeminiEmbeddingGenerator(http, apiKey, model, dimensions, snapshot.Endpoint);
    }

    private IEmbeddingGenerator<string, Embedding<float>> CreateVoyage(EmbeddingOptions snapshot, string model, int dimensions)
    {
        var apiKey = snapshot.ApiKey
            ?? Environment.GetEnvironmentVariable("VOYAGE_API_KEY")
            ?? Environment.GetEnvironmentVariable("ANTHROPIC_VOYAGE_API_KEY")
            ?? throw new InvalidOperationException(
                "Voyage embedding provider requires an ApiKey or VOYAGE_API_KEY.");

        var http = httpClientFactory.CreateClient("DeepRootVoyage");
        return new VoyageEmbeddingGenerator(http, apiKey, model, dimensions, snapshot.Endpoint);
    }

    private void DisposeCachedGenerator()
    {
        if (cachedGenerator is IDisposable disposable)
        {
            try { disposable.Dispose(); }
            catch { /* best-effort */ }
        }
    }
}
