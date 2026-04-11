using System.Collections.Concurrent;
using CompanyBrain.Dashboard.Data;
using CompanyBrain.Dashboard.Data.Models;
using Microsoft.EntityFrameworkCore;

namespace CompanyBrain.Dashboard.Services;

/// <summary>
/// Thread-safe settings service with in-memory caching for high-performance MCP tool execution.
/// Uses IDbContextFactory for thread safety in concurrent scenarios.
/// </summary>
public sealed class SettingsService : IDisposable
{
    private readonly IDbContextFactory<DocumentAssignmentDbContext> _contextFactory;
    private readonly ILogger<SettingsService> _logger;
    private readonly SemaphoreSlim _cacheLock = new(1, 1);
    
    // In-memory cache for settings
    private volatile AppSettings? _cachedSettings;
    private DateTime _cacheExpiry = DateTime.MinValue;
    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(5);

    public SettingsService(
        IDbContextFactory<DocumentAssignmentDbContext> contextFactory,
        ILogger<SettingsService> logger)
    {
        _contextFactory = contextFactory;
        _logger = logger;
    }

    /// <summary>
    /// Gets the current settings from cache or database.
    /// This method is optimized for high-throughput MCP tool execution.
    /// </summary>
    public async Task<AppSettings> GetSettingsAsync(CancellationToken cancellationToken = default)
    {
        // Fast path: return cached settings if valid
        if (_cachedSettings is not null && DateTime.UtcNow < _cacheExpiry)
        {
            return _cachedSettings;
        }

        await _cacheLock.WaitAsync(cancellationToken);
        try
        {
            // Double-check after acquiring lock
            if (_cachedSettings is not null && DateTime.UtcNow < _cacheExpiry)
            {
                return _cachedSettings;
            }

            await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
            
            var settings = await context.AppSettings
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.Id == AppSettingsConstants.SingletonId, cancellationToken);

            if (settings is null)
            {
                // Create default settings if none exist
                settings = new AppSettings { Id = AppSettingsConstants.SingletonId };
                context.AppSettings.Add(settings);
                await context.SaveChangesAsync(cancellationToken);
                _logger.LogInformation("Created default application settings");
            }

            _cachedSettings = settings;
            _cacheExpiry = DateTime.UtcNow.Add(CacheDuration);
            
            _logger.LogDebug("Settings loaded from database and cached");
            return settings;
        }
        finally
        {
            _cacheLock.Release();
        }
    }

    /// <summary>
    /// Gets settings synchronously from cache. Returns null if not cached.
    /// Use this for synchronous code paths where blocking is acceptable.
    /// </summary>
    public AppSettings? GetCachedSettings()
    {
        if (_cachedSettings is not null && DateTime.UtcNow < _cacheExpiry)
        {
            return _cachedSettings;
        }
        return null;
    }

    /// <summary>
    /// Updates the application settings and invalidates the cache.
    /// </summary>
    public async Task<AppSettings> UpdateSettingsAsync(
        Action<AppSettings> updateAction,
        CancellationToken cancellationToken = default)
    {
        await _cacheLock.WaitAsync(cancellationToken);
        try
        {
            await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
            
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
            
            // Update cache with new settings
            _cachedSettings = settings;
            _cacheExpiry = DateTime.UtcNow.Add(CacheDuration);
            
            _logger.LogInformation("Application settings updated successfully");
            return settings;
        }
        finally
        {
            _cacheLock.Release();
        }
    }

    /// <summary>
    /// Forces cache invalidation. Call this when external changes occur.
    /// </summary>
    public void InvalidateCache()
    {
        _cachedSettings = null;
        _cacheExpiry = DateTime.MinValue;
        _logger.LogDebug("Settings cache invalidated");
    }

    /// <summary>
    /// Checks if PII masking is currently enabled.
    /// Uses cached settings for high performance.
    /// </summary>
    public async Task<bool> IsPiiMaskingEnabledAsync(CancellationToken cancellationToken = default)
    {
        var settings = await GetSettingsAsync(cancellationToken);
        return settings.EnablePiiMasking;
    }

    /// <summary>
    /// Gets the system prompt prefix if configured.
    /// </summary>
    public async Task<string?> GetSystemPromptPrefixAsync(CancellationToken cancellationToken = default)
    {
        var settings = await GetSettingsAsync(cancellationToken);
        return string.IsNullOrWhiteSpace(settings.SystemPromptPrefix) 
            ? null 
            : settings.SystemPromptPrefix;
    }

    /// <summary>
    /// Gets the current security mode.
    /// </summary>
    public async Task<string> GetSecurityModeAsync(CancellationToken cancellationToken = default)
    {
        var settings = await GetSettingsAsync(cancellationToken);
        return settings.SecurityMode;
    }

    /// <summary>
    /// Gets excluded patterns as an array.
    /// </summary>
    public async Task<string[]> GetExcludedPatternsAsync(CancellationToken cancellationToken = default)
    {
        var settings = await GetSettingsAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(settings.ExcludedPatterns))
        {
            return [];
        }
        return settings.ExcludedPatterns
            .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }

    public void Dispose()
    {
        _cacheLock.Dispose();
    }
}
