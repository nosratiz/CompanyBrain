using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace CompanyBrain.Search.Vector;

/// <summary>
/// Decorator that consults <see cref="EmbeddingCache"/> before calling the underlying provider.
/// </summary>
public sealed class CachingEmbeddingGenerator : IEmbeddingGenerator<string, Embedding<float>>
{
    private readonly IEmbeddingGenerator<string, Embedding<float>> inner;
    private readonly EmbeddingCache cache;
    private readonly string model;
    private readonly int dimensions;
    private readonly ILogger<CachingEmbeddingGenerator> logger;

    public CachingEmbeddingGenerator(
        IEmbeddingGenerator<string, Embedding<float>> inner,
        EmbeddingCache cache,
        string model,
        int dimensions,
        ILogger<CachingEmbeddingGenerator> logger)
    {
        this.inner = inner;
        this.cache = cache;
        this.model = model;
        this.dimensions = dimensions;
        this.logger = logger;
    }

    public async Task<GeneratedEmbeddings<Embedding<float>>> GenerateAsync(
        IEnumerable<string> values,
        EmbeddingGenerationOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var inputs = values as IList<string> ?? [.. values];
        var results = new Embedding<float>?[inputs.Count];
        var misses = new List<int>();

        for (var i = 0; i < inputs.Count; i++)
        {
            var cached = await cache.TryGetAsync(inputs[i], model, dimensions, cancellationToken).ConfigureAwait(false);
            if (cached is not null)
            {
                results[i] = new Embedding<float>(cached);
            }
            else
            {
                misses.Add(i);
            }
        }

        if (misses.Count > 0)
        {
            logger.LogDebug("Embedding cache: {Hits}/{Total} hit, {Misses} fetched from provider.",
                inputs.Count - misses.Count, inputs.Count, misses.Count);

            var missInputs = misses.Select(i => inputs[i]).ToArray();
            var freshly = await inner.GenerateAsync(missInputs, options, cancellationToken).ConfigureAwait(false);

            for (var j = 0; j < misses.Count; j++)
            {
                var idx = misses[j];
                var emb = freshly[j];
                results[idx] = emb;
                await cache.SetAsync(inputs[idx], model, dimensions, emb.Vector.ToArray(), cancellationToken).ConfigureAwait(false);
            }
        }

        var output = new GeneratedEmbeddings<Embedding<float>>();
        foreach (var r in results)
        {
            output.Add(r!);
        }
        return output;
    }

    public object? GetService(Type serviceType, object? serviceKey = null)
        => inner.GetService(serviceType, serviceKey);

    public void Dispose() => inner.Dispose();
}
