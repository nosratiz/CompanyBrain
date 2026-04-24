using System.Collections.Concurrent;

namespace DeepRoot.Shared.Services;

/// <summary>
/// Default in-process implementation of <see cref="IStorageService"/>.
/// Persists nothing on its own — the SQLite-backed
/// <c>SettingsRepository</c> is the durable store. This service exists so
/// transient session values can be passed between Razor components and
/// the MCP server without a round-trip to disk.
/// </summary>
public sealed class LocalStorageService : IStorageService
{
    private readonly ConcurrentDictionary<string, string> _store = new(StringComparer.Ordinal);

    public Task<string?> GetAsync(string key, CancellationToken ct = default)
        => Task.FromResult(_store.TryGetValue(key, out var v) ? v : null);

    public Task SetAsync(string key, string value, CancellationToken ct = default)
    {
        _store[key] = value;
        return Task.CompletedTask;
    }

    public Task RemoveAsync(string key, CancellationToken ct = default)
    {
        _store.TryRemove(key, out _);
        return Task.CompletedTask;
    }
}
