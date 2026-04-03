using System.Security.Cryptography;

namespace CompanyBrain.MultiTenant.Domain;

/// <summary>
/// API Key for external MCP clients (Claude Desktop, GitHub Copilot, etc.)
/// </summary>
public sealed class ApiKey
{
    private const string Prefix = "cb_";
    private const int KeyLength = 32;

    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid TenantId { get; init; }
    public required string Name { get; set; }

    /// <summary>
    /// The hashed key value. The plain key is only shown once at creation.
    /// </summary>
    public required string KeyHash { get; init; }

    /// <summary>
    /// Key prefix for identification (e.g., "cb_abc123...")
    /// </summary>
    public required string KeyPrefix { get; init; }

    public ApiKeyScope Scope { get; set; } = ApiKeyScope.ReadOnly;
    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;
    public DateTime? ExpiresAt { get; set; }
    public DateTime? LastUsedAt { get; set; }
    public bool IsRevoked { get; set; }
    public string? RevokedReason { get; set; }

    // Rate limiting
    public int RequestsPerMinute { get; set; } = 60;
    public int RequestsPerDay { get; set; } = 10_000;

    // Navigation
    public Tenant? Tenant { get; init; }

    /// <summary>
    /// Generates a new API key with the company brain prefix.
    /// Returns the plain key (show once) and the entity with hashed key.
    /// </summary>
    public static (string PlainKey, ApiKey Entity) Generate(Guid tenantId, string name, ApiKeyScope scope = ApiKeyScope.ReadOnly, DateTime? expiresAt = null)
    {
        var randomBytes = RandomNumberGenerator.GetBytes(KeyLength);
        var plainKey = $"{Prefix}{Convert.ToBase64String(randomBytes).Replace("+", "-").Replace("/", "_").TrimEnd('=')}";
        var keyHash = HashKey(plainKey);
        var keyPrefix = plainKey[..(Prefix.Length + 8)];

        var entity = new ApiKey
        {
            TenantId = tenantId,
            Name = name,
            KeyHash = keyHash,
            KeyPrefix = keyPrefix,
            Scope = scope,
            ExpiresAt = expiresAt
        };

        return (plainKey, entity);
    }

    /// <summary>
    /// Hashes an API key using SHA256.
    /// </summary>
    public static string HashKey(string plainKey)
    {
        var bytes = System.Text.Encoding.UTF8.GetBytes(plainKey);
        var hash = SHA256.HashData(bytes);
        return Convert.ToBase64String(hash);
    }

    /// <summary>
    /// Validates if the key is currently usable.
    /// </summary>
    public bool IsValid() =>
        !IsRevoked &&
        (ExpiresAt is null || ExpiresAt > DateTime.UtcNow);
}

[Flags]
public enum ApiKeyScope
{
    ReadOnly = 1,
    WriteDocuments = 2,
    ManageResources = 4,
    Admin = ReadOnly | WriteDocuments | ManageResources
}
