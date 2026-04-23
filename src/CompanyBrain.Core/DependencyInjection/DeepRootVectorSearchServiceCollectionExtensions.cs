using CompanyBrain.Search.Vector;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CompanyBrain.DependencyInjection;

public static class DeepRootVectorSearchServiceCollectionExtensions
{
    /// <summary>
    /// Wires the DeepRoot vector search subsystem. Registration is unconditional — the runtime
    /// only contacts the embedding provider when an <see cref="IEmbeddingOptionsAccessor"/>
    /// returns a non-<see cref="EmbeddingProviderType.None"/> provider, so this is safe to call
    /// even when no embedding settings have been configured yet.
    /// </summary>
    /// <param name="knowledgeRootPath">
    /// Used to derive the default vector database path
    /// (<c>{knowledgeRoot}/.deeproot/vectors.db</c>) when the accessor doesn't supply one.
    /// </param>
    public static IServiceCollection AddDeepRootVectorSearch(
        this IServiceCollection services,
        string knowledgeRootPath)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentException.ThrowIfNullOrWhiteSpace(knowledgeRootPath);

        var defaultDbPath = Path.Combine(knowledgeRootPath, ".deeproot", "vectors.db");
        var defaultDbDir = Path.GetDirectoryName(defaultDbPath);
        if (!string.IsNullOrWhiteSpace(defaultDbDir))
        {
            Directory.CreateDirectory(defaultDbDir);
        }

        services.AddOptions<EmbeddingOptions>();

        services.AddHttpClient("DeepRootGemini", client => client.Timeout = TimeSpan.FromSeconds(30));
        services.AddHttpClient("DeepRootVoyage", client => client.Timeout = TimeSpan.FromSeconds(30));

        // Default accessor binds to IOptions<EmbeddingOptions>. The Dashboard layer replaces this
        // with a database-backed accessor (see DatabaseEmbeddingOptionsAccessor).
        services.TryAddSingleton<IEmbeddingOptionsAccessor, OptionsBackedEmbeddingOptionsAccessor>();

        services.TryAddSingleton(_ => new EmbeddingCache(BuildConnectionString(defaultDbPath)));
        services.TryAddSingleton(sp =>
        {
            var factory = ActivatorUtilities.CreateInstance<EmbeddingProviderFactory>(sp);
            factory.DefaultDatabasePath = defaultDbPath;
            return factory;
        });
        services.TryAddSingleton<DocumentEmbeddingIndexer>();

        return services;
    }

    private static string BuildConnectionString(string dbPath)
    {
        return new Microsoft.Data.Sqlite.SqliteConnectionStringBuilder
        {
            DataSource = dbPath,
            Mode = Microsoft.Data.Sqlite.SqliteOpenMode.ReadWriteCreate,
            Cache = Microsoft.Data.Sqlite.SqliteCacheMode.Shared,
        }.ToString();
    }
}
