namespace DeepRoot.Shared.Services;

/// <summary>
/// Abstraction over per-user, per-platform key/value storage.
/// Implementations may target the local file system, secure enclaves,
/// or remote sync backends. The DeepRoot.Photino shell uses
/// <see cref="LocalStorageService"/>.
/// </summary>
public interface IStorageService
{
    Task<string?> GetAsync(string key, CancellationToken ct = default);
    Task SetAsync(string key, string value, CancellationToken ct = default);
    Task RemoveAsync(string key, CancellationToken ct = default);
}
