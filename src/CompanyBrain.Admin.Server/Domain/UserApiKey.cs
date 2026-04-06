using System.Security.Cryptography;
using System.Text;
using CompanyBrain.Admin.Server.Domain.Enums;
using FluentResults;

namespace CompanyBrain.Admin.Server.Domain;

public sealed class UserApiKey
{
    private const string Prefix = "cb_";
    private const int KeyLength = 32;

    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public required string Name { get; set; }
    public required string KeyHash { get; set; }
    public required string KeyPrefix { get; set; }
    public ApiKeyScope Scope { get; set; } = ApiKeyScope.ReadOnly;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ExpiresAt { get; set; }
    public DateTime? LastUsedAt { get; set; }
    public bool IsRevoked { get; set; } = false;
    public string? RevokedReason { get; set; }

    public User? User { get; set; }

    public static (string PlainKey, UserApiKey ApiKey) Create(Guid userId, string name, ApiKeyScope scope, DateTime? expiresAt)
    {
        var randomBytes = RandomNumberGenerator.GetBytes(KeyLength);
        var keyPart = Convert.ToBase64String(randomBytes)
            .Replace("+", "-")
            .Replace("/", "_")
            .TrimEnd('=');

        var plainKey = $"{Prefix}{keyPart}";

        return (plainKey, new UserApiKey
        {
            UserId = userId,
            Name = name,
            KeyHash = HashKey(plainKey),
            KeyPrefix = plainKey[..(Prefix.Length + 8)],
            Scope = scope,
            ExpiresAt = expiresAt.HasValue ? DateTime.SpecifyKind(expiresAt.Value, DateTimeKind.Utc) : null
        });
    }

    public Result ValidateForUse(bool isUserActive, DateTime utcNow)
    {
        if (IsRevoked)
        {
            return Result.Fail("API key has been revoked");
        }

        if (ExpiresAt.HasValue && ExpiresAt < utcNow)
        {
            return Result.Fail("API key has expired");
        }

        if (!isUserActive)
        {
            return Result.Fail("User account is not active");
        }

        return Result.Ok();
    }

    public void MarkUsed(DateTime utcNow) => LastUsedAt = utcNow;

    public void Revoke(string reason)
    {
        IsRevoked = true;
        RevokedReason = reason;
    }

    public static string HashKey(string plainKey)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(plainKey));
        return Convert.ToBase64String(bytes);
    }
}