using System.Collections.Concurrent;
using CompanyBrain.Dashboard.Data;
using CompanyBrain.Dashboard.Data.Audit;
using CompanyBrain.Dashboard.Data.Models;
using CompanyBrain.Dashboard.Services.Audit;
using Microsoft.EntityFrameworkCore;

namespace CompanyBrain.Dashboard.Services;

/// <summary>
/// Thread-safe settings service with in-memory caching for high-performance MCP tool execution.
/// Uses IDbContextFactory for thread safety in concurrent scenarios.
/// </summary>
public sealed class SettingsService(
    IDbContextFactory<DocumentAssignmentDbContext> contextFactory,
    IAuditService audit,
    ILogger<SettingsService> logger) : IDisposable
{
    private readonly SemaphoreSlim _cacheLock = new(1, 1);
    
    private volatile AppSettings? _cachedSettings;
    private DateTime _cacheExpiry = DateTime.MinValue;
    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(5);

    public async Task<AppSettings> GetSettingsAsync(CancellationToken cancellationToken = default)
    {
        if (TryGetCachedSettings() is { } cached)
            return cached;

        await _cacheLock.WaitAsync(cancellationToken);
        try
        {
            if (TryGetCachedSettings() is { } doubleChecked)
                return doubleChecked;

            var settings = await LoadOrCreateSettingsAsync(cancellationToken);
            UpdateCache(settings);
            
            logger.LogDebug("Settings loaded from database and cached");
            return settings;
        }
        finally
        {
            _cacheLock.Release();
        }
    }

    public AppSettings? GetCachedSettings() => TryGetCachedSettings();

    public async Task<AppSettings> UpdateSettingsAsync(
        Action<AppSettings> updateAction,
        CancellationToken cancellationToken = default)
    {
        await _cacheLock.WaitAsync(cancellationToken);
        try
        {
            await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
            
            var settings = await context.AppSettings
                .FirstOrDefaultAsync(s => s.Id == AppSettingsConstants.SingletonId, cancellationToken);

            if (settings is null)
            {
                settings = new AppSettings { Id = AppSettingsConstants.SingletonId };
                context.AppSettings.Add(settings);
            }

            updateAction(settings);
            settings.UpdatedAtUtc = DateTime.UtcNow;
            
            await context.SaveChangesAsync(cancellationToken);
            UpdateCache(settings);

            logger.LogInformation("Application settings updated successfully");

            _ = audit.LogAsync(AuditEventType.SettingsChanged, new AuditEntry(
                ActorId: "system",
                ResourceType: "Settings",
                ResourceName: "AppSettings"));

            return settings;
        }
        finally
        {
            _cacheLock.Release();
        }
    }

    public void InvalidateCache()
    {
        _cachedSettings = null;
        _cacheExpiry = DateTime.MinValue;
        logger.LogDebug("Settings cache invalidated");
    }

    public async Task<bool> IsPiiMaskingEnabledAsync(CancellationToken cancellationToken = default)
    {
        var settings = await GetSettingsAsync(cancellationToken);
        return settings.EnablePiiMasking;
    }

    public async Task<string?> GetSystemPromptPrefixAsync(CancellationToken cancellationToken = default)
    {
        var settings = await GetSettingsAsync(cancellationToken);
        return string.IsNullOrWhiteSpace(settings.SystemPromptPrefix) 
            ? null 
            : settings.SystemPromptPrefix;
    }

    public async Task<string> GetSecurityModeAsync(CancellationToken cancellationToken = default)
    {
        var settings = await GetSettingsAsync(cancellationToken);
        return settings.SecurityMode;
    }

    public async Task<string[]> GetExcludedPatternsAsync(CancellationToken cancellationToken = default)
    {
        var settings = await GetSettingsAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(settings.ExcludedPatterns))
            return [];

        return settings.ExcludedPatterns
            .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }

    public void Dispose() => _cacheLock.Dispose();

    // ── Private helpers ─────────────────────────────────────────────

    private AppSettings? TryGetCachedSettings()
    {
        return _cachedSettings is not null && DateTime.UtcNow < _cacheExpiry
            ? _cachedSettings
            : null;
    }

    private void UpdateCache(AppSettings settings)
    {
        _cachedSettings = settings;
        _cacheExpiry = DateTime.UtcNow.Add(CacheDuration);
    }

    private async Task<AppSettings> LoadOrCreateSettingsAsync(CancellationToken cancellationToken)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
            
        var settings = await context.AppSettings
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == AppSettingsConstants.SingletonId, cancellationToken);

        if (settings is not null)
            return settings;

        settings = new AppSettings { Id = AppSettingsConstants.SingletonId };
        context.AppSettings.Add(settings);
        await context.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Created default application settings");
        return settings;
    }
}
