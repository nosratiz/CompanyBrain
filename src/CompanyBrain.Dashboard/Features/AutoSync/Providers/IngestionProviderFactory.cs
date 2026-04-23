using CompanyBrain.Dashboard.Features.AutoSync.Models;

namespace CompanyBrain.Dashboard.Features.AutoSync.Providers;

/// <summary>
/// Resolves the correct <see cref="IIngestionProvider"/> for a given <see cref="SourceType"/>.
///
/// <para>
/// Providers are registered in DI as <see cref="IIngestionProvider"/> with multiple
/// implementations; this factory collects them all and builds an O(1) lookup dictionary
/// at construction time.  New providers are picked up automatically once registered.
/// </para>
/// </summary>
public sealed class IngestionProviderFactory
{
    private readonly IReadOnlyDictionary<SourceType, IIngestionProvider> _index;

    public IngestionProviderFactory(IEnumerable<IIngestionProvider> providers)
    {
        _index = providers.ToDictionary(p => p.SourceType);
    }

    /// <summary>
    /// Returns the provider for <paramref name="sourceType"/>, or
    /// <see langword="null"/> when no provider is registered.
    /// </summary>
    public IIngestionProvider? GetProvider(SourceType sourceType)
        => _index.GetValueOrDefault(sourceType);

    /// <summary>All registered source types.</summary>
    public IReadOnlyCollection<SourceType> RegisteredTypes => _index.Keys.ToArray();
}
