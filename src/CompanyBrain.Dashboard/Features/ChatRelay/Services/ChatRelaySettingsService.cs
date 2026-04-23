using CompanyBrain.Dashboard.Data;
using CompanyBrain.Dashboard.Data.Audit;
using CompanyBrain.Dashboard.Features.ChatRelay.Models;
using CompanyBrain.Dashboard.Services.Audit;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;

namespace CompanyBrain.Dashboard.Features.ChatRelay.Services;

/// <summary>
/// CRUD service for the singleton <see cref="ChatBotSettings"/> row.
/// Tokens and secrets are encrypted with ASP.NET Data Protection on write
/// and decrypted on read — plaintext credentials never reach disk.
/// </summary>
public sealed class ChatRelaySettingsService
{
    private const string ProtectorPurpose = "CompanyBrain.ChatRelay.BotTokens.v1";

    // 30-second in-memory cache so every incoming webhook doesn't hit SQLite.
    private static readonly TimeSpan CacheDuration = TimeSpan.FromSeconds(30);

    private readonly IDbContextFactory<DocumentAssignmentDbContext> _contextFactory;
    private readonly IDataProtector _protector;
    private readonly IAuditService _audit;
    private readonly ILogger<ChatRelaySettingsService> _logger;
    private readonly SemaphoreSlim _lock = new(1, 1);

    private volatile ChatBotSettings? _cache;
    private DateTime _cacheExpiry = DateTime.MinValue;

    // Decrypted secrets cached alongside settings (never persisted to DB in plain form).
    private volatile DecryptedSecrets? _decryptedCache;

    public ChatRelaySettingsService(
        IDbContextFactory<DocumentAssignmentDbContext> contextFactory,
        IDataProtectionProvider dataProtectionProvider,
        IAuditService audit,
        ILogger<ChatRelaySettingsService> logger)
    {
        _contextFactory = contextFactory;
        _protector = dataProtectionProvider.CreateProtector(ProtectorPurpose);
        _audit = audit;
        _logger = logger;
    }

    /// <summary>
    /// Returns the raw <see cref="ChatBotSettings"/> row (encrypted fields are NOT decrypted).
    /// Use this for the Settings UI to avoid leaking secrets.
    /// </summary>
    public async Task<ChatBotSettings> GetSettingsAsync(CancellationToken ct = default)
    {
        if (TryGetCached(out var cached))
            return cached!;

        await _lock.WaitAsync(ct);
        try
        {
            if (TryGetCached(out cached))
                return cached!;

            cached = await LoadOrCreateAsync(ct);
            SetCache(cached);
            return cached;
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <summary>
    /// Returns decrypted Slack credentials.  Returns <c>null</c> fields when not configured.
    /// </summary>
    public async Task<(string? BotToken, string? SigningSecret)> GetSlackCredentialsAsync(CancellationToken ct = default)
    {
        if (TryGetDecryptedCache(out var dec))
            return (dec!.SlackBotToken, dec.SlackSigningSecret);

        var settings = await GetSettingsAsync(ct);
        return BuildDecryptedSecrets(settings);
    }

    /// <summary>
    /// Returns the decrypted Teams app password.  Returns <c>null</c> when not configured.
    /// </summary>
    public async Task<string?> GetTeamsAppPasswordAsync(CancellationToken ct = default)
    {
        if (TryGetDecryptedCache(out var dec))
            return dec!.TeamsAppPassword;

        var settings = await GetSettingsAsync(ct);
        var (_, _, pass) = BuildDecryptedSecretsTuple(settings);
        return pass;
    }

    /// <summary>
    /// Persists updated bot settings.  When a token/secret parameter is <c>null</c>,
    /// the existing encrypted value is preserved; an empty string clears it.
    /// </summary>
    public async Task<ChatBotSettings> UpdateAsync(
        bool slackEnabled,
        bool teamsEnabled,
        bool tunnelEnabled,
        string? slackBotToken,
        string? slackSigningSecret,
        string teamsAppId,
        string? teamsAppPassword,
        CancellationToken ct = default)
    {
        await _lock.WaitAsync(ct);
        try
        {
            await using var db = await _contextFactory.CreateDbContextAsync(ct);

            var row = await db.ChatBotSettings
                .FirstOrDefaultAsync(s => s.Id == ChatBotSettingsConstants.SingletonId, ct)
                ?? new ChatBotSettings { Id = ChatBotSettingsConstants.SingletonId };

            if (row.Id == ChatBotSettingsConstants.SingletonId && db.Entry(row).State == EntityState.Detached)
                db.ChatBotSettings.Add(row);

            row.SlackEnabled = slackEnabled;
            row.TeamsEnabled = teamsEnabled;
            row.TunnelEnabled = tunnelEnabled;
            row.TeamsAppId = teamsAppId ?? string.Empty;
            row.UpdatedAtUtc = DateTime.UtcNow;

            if (slackBotToken is not null)
                row.EncryptedSlackBotToken = string.IsNullOrEmpty(slackBotToken) ? string.Empty : _protector.Protect(slackBotToken);

            if (slackSigningSecret is not null)
                row.EncryptedSlackSigningSecret = string.IsNullOrEmpty(slackSigningSecret) ? string.Empty : _protector.Protect(slackSigningSecret);

            if (teamsAppPassword is not null)
                row.EncryptedTeamsAppPassword = string.IsNullOrEmpty(teamsAppPassword) ? string.Empty : _protector.Protect(teamsAppPassword);

            await db.SaveChangesAsync(ct);

            SetCache(row);
            _decryptedCache = null; // invalidate decrypted cache

            _logger.LogInformation(
                "ChatRelay settings updated — Slack={Slack}, Teams={Teams}, Tunnel={Tunnel}",
                slackEnabled, teamsEnabled, tunnelEnabled);

            _ = _audit.LogAsync(AuditEventType.SettingsChanged, new AuditEntry(
                ActorId: "system",
                ResourceType: "Settings",
                ResourceName: "ChatRelaySettings",
                Metadata: new { slackEnabled, teamsEnabled, tunnelEnabled, teamsAppId }));

            return row;
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <summary>Persists the current tunnel URL without touching other settings.</summary>
    public async Task UpdateTunnelUrlAsync(string tunnelUrl, CancellationToken ct = default)
    {
        await using var db = await _contextFactory.CreateDbContextAsync(ct);

        var row = await db.ChatBotSettings
            .FirstOrDefaultAsync(s => s.Id == ChatBotSettingsConstants.SingletonId, ct);

        if (row is null) return;

        row.TunnelUrl = tunnelUrl;
        row.UpdatedAtUtc = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);

        // Bust the in-memory cache so the UI picks up the new URL.
        InvalidateCache();
    }

    /// <summary>
    /// Persists the devtunnel ID that was created on first startup.
    /// Subsequent restarts reuse this ID so the public URL stays stable.
    /// </summary>
    public async Task UpdateDevTunnelIdAsync(string tunnelId, CancellationToken ct = default)
    {
        await using var db = await _contextFactory.CreateDbContextAsync(ct);

        var row = await db.ChatBotSettings
            .FirstOrDefaultAsync(s => s.Id == ChatBotSettingsConstants.SingletonId, ct);

        if (row is null) return;

        row.DevTunnelId = tunnelId;
        row.UpdatedAtUtc = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);

        InvalidateCache();
    }

    /// <summary>
    /// Clears the stored devtunnel ID and current tunnel URL so the next tunnel
    /// start creates a brand-new tunnel with a fresh public URL.
    /// </summary>
    public async Task ClearDevTunnelIdAsync(CancellationToken ct = default)
    {
        await using var db = await _contextFactory.CreateDbContextAsync(ct);

        var row = await db.ChatBotSettings
            .FirstOrDefaultAsync(s => s.Id == ChatBotSettingsConstants.SingletonId, ct);

        if (row is null) return;

        row.DevTunnelId = string.Empty;
        row.TunnelUrl = string.Empty;
        row.UpdatedAtUtc = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);

        InvalidateCache();
    }

    public void InvalidateCache()
    {
        _cache = null;
        _decryptedCache = null;
        _cacheExpiry = DateTime.MinValue;
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    private async Task<ChatBotSettings> LoadOrCreateAsync(CancellationToken ct)
    {
        await using var db = await _contextFactory.CreateDbContextAsync(ct);

        var row = await db.ChatBotSettings
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == ChatBotSettingsConstants.SingletonId, ct);

        if (row is not null) return row;

        row = new ChatBotSettings { Id = ChatBotSettingsConstants.SingletonId };
        db.ChatBotSettings.Add(row);
        await db.SaveChangesAsync(ct);
        return row;
    }

    private bool TryGetCached(out ChatBotSettings? settings)
    {
        if (_cache is not null && DateTime.UtcNow < _cacheExpiry)
        {
            settings = _cache;
            return true;
        }
        settings = null;
        return false;
    }

    private bool TryGetDecryptedCache(out DecryptedSecrets? dec)
    {
        if (_decryptedCache is not null && DateTime.UtcNow < _cacheExpiry)
        {
            dec = _decryptedCache;
            return true;
        }
        dec = null;
        return false;
    }

    private void SetCache(ChatBotSettings settings)
    {
        _cache = settings;
        _decryptedCache = null;
        _cacheExpiry = DateTime.UtcNow.Add(CacheDuration);
    }

    private (string? BotToken, string? SigningSecret) BuildDecryptedSecrets(ChatBotSettings settings)
    {
        var botToken = Decrypt(settings.EncryptedSlackBotToken);
        var signing = Decrypt(settings.EncryptedSlackSigningSecret);
        var pass = Decrypt(settings.EncryptedTeamsAppPassword);
        _decryptedCache = new DecryptedSecrets(botToken, signing, pass);
        return (botToken, signing);
    }

    private (string? BotToken, string? SigningSecret, string? AppPassword) BuildDecryptedSecretsTuple(ChatBotSettings settings)
    {
        var botToken = Decrypt(settings.EncryptedSlackBotToken);
        var signing = Decrypt(settings.EncryptedSlackSigningSecret);
        var pass = Decrypt(settings.EncryptedTeamsAppPassword);
        _decryptedCache = new DecryptedSecrets(botToken, signing, pass);
        return (botToken, signing, pass);
    }

    private string? Decrypt(string encrypted)
    {
        if (string.IsNullOrEmpty(encrypted)) return null;
        try { return _protector.Unprotect(encrypted); }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to decrypt a ChatRelay credential — it may have been created with a different key");
            return null;
        }
    }

    private sealed record DecryptedSecrets(string? SlackBotToken, string? SlackSigningSecret, string? TeamsAppPassword);
}
