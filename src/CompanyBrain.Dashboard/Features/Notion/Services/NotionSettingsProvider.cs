using CompanyBrain.Dashboard.Data;
using CompanyBrain.Dashboard.Data.Models;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;

namespace CompanyBrain.Dashboard.Features.Notion.Services;

/// <summary>
/// Reads and writes the Notion API token, using ASP.NET Data Protection for encryption at rest.
/// The plaintext token is never persisted to the database directly.
/// </summary>
public sealed class NotionSettingsProvider
{
    private const string ProtectorPurpose = "CompanyBrain.Notion.ApiToken.v1";

    private readonly IDbContextFactory<DocumentAssignmentDbContext> _contextFactory;
    private readonly IDataProtector _protector;
    private readonly ILogger<NotionSettingsProvider> _logger;

    public NotionSettingsProvider(
        IDbContextFactory<DocumentAssignmentDbContext> contextFactory,
        IDataProtectionProvider dataProtectionProvider,
        ILogger<NotionSettingsProvider> logger)
    {
        _contextFactory = contextFactory;
        _protector = dataProtectionProvider.CreateProtector(ProtectorPurpose);
        _logger = logger;
    }

    /// <summary>
    /// Returns the decrypted Notion API token, or <c>null</c> when none is stored.
    /// </summary>
    public async Task<string?> GetDecryptedTokenAsync(CancellationToken ct = default)
    {
        var row = await LoadRowAsync(ct).ConfigureAwait(false);

        if (string.IsNullOrEmpty(row.NotionApiToken))
            return null;

        try
        {
            return _protector.Unprotect(row.NotionApiToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to decrypt Notion API token; returning null");
            return null;
        }
    }

    /// <summary>
    /// Persists the (encrypted) token and workspace filter to the singleton AppSettings row.
    /// </summary>
    public async Task SaveAsync(string rawToken, string workspaceFilter, CancellationToken ct = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(ct).ConfigureAwait(false);

        var row = await context.AppSettings
            .FirstOrDefaultAsync(s => s.Id == AppSettingsConstants.SingletonId, ct)
            .ConfigureAwait(false);

        if (row is null)
        {
            row = new AppSettings { Id = AppSettingsConstants.SingletonId };
            context.AppSettings.Add(row);
        }

        row.NotionApiToken = string.IsNullOrEmpty(rawToken)
            ? string.Empty
            : _protector.Protect(rawToken);

        row.NotionWorkspaceFilter = workspaceFilter ?? string.Empty;
        row.UpdatedAtUtc = DateTime.UtcNow;

        await context.SaveChangesAsync(ct).ConfigureAwait(false);

        _logger.LogInformation("Notion settings saved (token present: {HasToken})", !string.IsNullOrEmpty(rawToken));
    }

    /// <summary>
    /// Returns the workspace filter and whether a token is currently stored,
    /// without exposing the plaintext token.
    /// </summary>
    public async Task<(string WorkspaceFilter, bool HasToken)> GetConfigAsync(CancellationToken ct = default)
    {
        var row = await LoadRowAsync(ct).ConfigureAwait(false);
        return (row.NotionWorkspaceFilter, !string.IsNullOrEmpty(row.NotionApiToken));
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private async Task<AppSettings> LoadRowAsync(CancellationToken ct)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
        return await context.AppSettings
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == AppSettingsConstants.SingletonId, ct)
            .ConfigureAwait(false)
            ?? new AppSettings();
    }
}
