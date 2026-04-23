using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;

namespace CompanyBrain.Dashboard.Mcp.Collections;

public sealed class CollectionEntitlementsStore(
    IDataProtectionProvider dataProtectionProvider,
    IWebHostEnvironment environment,
    ILogger<CollectionEntitlementsStore> logger)
{
    private const string ProtectorPurpose = "CompanyBrain.CollectionEntitlements.v1";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly IDataProtector _protector = dataProtectionProvider.CreateProtector(ProtectorPurpose);

    public async Task SaveAsync(CollectionEntitlementsManifest manifest, CancellationToken cancellationToken)
    {
        var path = GetCachePath();
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);

        var payload = JsonSerializer.SerializeToUtf8Bytes(manifest, JsonOptions);
        var protectedPayload = _protector.Protect(payload);
        await File.WriteAllBytesAsync(path, protectedPayload, cancellationToken);
    }

    public async Task<CollectionEntitlementsManifest?> TryLoadAsync(CancellationToken cancellationToken)
    {
        var path = GetCachePath();
        if (!File.Exists(path))
        {
            return null;
        }

        try
        {
            var protectedPayload = await File.ReadAllBytesAsync(path, cancellationToken);
            var payload = _protector.Unprotect(protectedPayload);
            return JsonSerializer.Deserialize<CollectionEntitlementsManifest>(payload, JsonOptions);
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Failed to read protected collection entitlements cache. Re-fetching from cloud.");
            return null;
        }
    }

    private string GetCachePath()
        => Path.Combine(environment.ContentRootPath, "db", "protected", "collection-entitlements.cache");
}