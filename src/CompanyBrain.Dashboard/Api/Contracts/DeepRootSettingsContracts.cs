namespace CompanyBrain.Dashboard.Api.Contracts;

/// <summary>
/// Read-only view of the saved DeepRoot embedding settings. The API key is never returned —
/// the UI only learns whether one is configured via <see cref="HasApiKey"/>.
/// </summary>
public sealed record DeepRootSettingsResponse(
    string Provider,
    string Model,
    int Dimensions,
    bool HasApiKey,
    string Endpoint,
    string DatabasePath,
    DateTime UpdatedAtUtc,
    string ResolvedModel,
    int ResolvedDimensions,
    bool ProviderActive);

/// <summary>
/// Result returned by <c>POST /api/deeproot/settings/test</c>.
/// </summary>
public sealed record DeepRootTestResponse(
    bool Success,
    string? Error,
    long? ElapsedMs,
    int? EmbeddingDimensions);

/// <summary>
/// Update payload for the DeepRoot embedding settings. <see cref="ApiKey"/> is optional:
/// <c>null</c> = leave existing key unchanged, empty string = clear the key, non-empty = replace.
/// </summary>
public sealed record DeepRootSettingsUpdateRequest(
    string Provider,
    string? Model,
    int? Dimensions,
    string? ApiKey,
    string? Endpoint,
    string? DatabasePath);
