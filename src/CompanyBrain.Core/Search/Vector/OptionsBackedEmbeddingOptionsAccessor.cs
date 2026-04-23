using Microsoft.Extensions.Options;

namespace CompanyBrain.Search.Vector;

/// <summary>
/// Default <see cref="IEmbeddingOptionsAccessor"/> backed by <c>IOptionsMonitor&lt;EmbeddingOptions&gt;</c>.
/// Used when the caller has not supplied a runtime/database accessor.
/// </summary>
public sealed class OptionsBackedEmbeddingOptionsAccessor : IEmbeddingOptionsAccessor
{
    private readonly IOptionsMonitor<EmbeddingOptions> monitor;

    public OptionsBackedEmbeddingOptionsAccessor(IOptionsMonitor<EmbeddingOptions> monitor)
    {
        this.monitor = monitor;
    }

    public EmbeddingOptions GetCurrent()
    {
        var current = monitor.CurrentValue;
        return new EmbeddingOptions
        {
            Provider = current.Provider,
            Model = current.Model,
            Dimensions = current.Dimensions,
            ApiKey = current.ApiKey,
            Endpoint = current.Endpoint,
            DatabasePath = current.DatabasePath,
        };
    }
}
