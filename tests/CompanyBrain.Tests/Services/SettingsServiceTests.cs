using CompanyBrain.Dashboard.Data;
using CompanyBrain.Dashboard.Data.Models;
using CompanyBrain.Dashboard.Services;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace CompanyBrain.Tests.Services;

public sealed class SettingsServiceTests : IDisposable
{
    private readonly IDbContextFactory<DocumentAssignmentDbContext> _contextFactory;
    private readonly ILogger<SettingsService> _logger;
    private readonly DbContextOptions<DocumentAssignmentDbContext> _options;
    private readonly SettingsService _sut;

    public SettingsServiceTests()
    {
        _options = new DbContextOptionsBuilder<DocumentAssignmentDbContext>()
            .UseInMemoryDatabase($"SettingsTests_{Guid.NewGuid():N}")
            .Options;
        
        _contextFactory = new TestDbContextFactory(_options);
        _logger = Substitute.For<ILogger<SettingsService>>();
        _sut = new SettingsService(_contextFactory, _logger);
    }

    #region GetSettingsAsync Tests

    [Fact]
    public async Task GetSettingsAsync_WhenNoSettingsExist_ShouldCreateDefault()
    {
        var settings = await _sut.GetSettingsAsync();

        settings.Should().NotBeNull();
        settings.Id.Should().Be(AppSettingsConstants.SingletonId);
        settings.EnablePiiMasking.Should().BeFalse();
        settings.SecurityMode.Should().Be("Moderate");
        settings.MaxStorageGb.Should().Be(10);
    }

    [Fact]
    public async Task GetSettingsAsync_WhenSettingsExist_ShouldReturnExisting()
    {
        // Arrange - Create existing settings
        await using (var ctx = new DocumentAssignmentDbContext(_options))
        {
            ctx.AppSettings.Add(new AppSettings
            {
                Id = AppSettingsConstants.SingletonId,
                EnablePiiMasking = true,
                SecurityMode = "Strict",
                MaxStorageGb = 50
            });
            await ctx.SaveChangesAsync();
        }

        // Act
        var settings = await _sut.GetSettingsAsync();

        // Assert
        settings.EnablePiiMasking.Should().BeTrue();
        settings.SecurityMode.Should().Be("Strict");
        settings.MaxStorageGb.Should().Be(50);
    }

    [Fact]
    public async Task GetSettingsAsync_ShouldCacheResult()
    {
        // First call creates settings
        var first = await _sut.GetSettingsAsync();
        
        // Second call should return cached
        var second = await _sut.GetSettingsAsync();

        first.Should().BeSameAs(second);
    }

    #endregion

    #region GetCachedSettings Tests

    [Fact]
    public void GetCachedSettings_WhenNotCached_ShouldReturnNull()
    {
        var result = _sut.GetCachedSettings();
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetCachedSettings_WhenCached_ShouldReturnSettings()
    {
        // Prime the cache
        await _sut.GetSettingsAsync();

        var result = _sut.GetCachedSettings();

        result.Should().NotBeNull();
        result!.Id.Should().Be(AppSettingsConstants.SingletonId);
    }

    #endregion

    #region UpdateSettingsAsync Tests

    [Fact]
    public async Task UpdateSettingsAsync_ShouldPersistChanges()
    {
        // Act
        var updated = await _sut.UpdateSettingsAsync(s =>
        {
            s.EnablePiiMasking = true;
            s.SecurityMode = "Strict";
            s.SystemPromptPrefix = "Test prefix";
        });

        // Assert
        updated.EnablePiiMasking.Should().BeTrue();
        updated.SecurityMode.Should().Be("Strict");
        updated.SystemPromptPrefix.Should().Be("Test prefix");

        // Verify persisted
        await using var ctx = new DocumentAssignmentDbContext(_options);
        var persisted = await ctx.AppSettings.FindAsync(AppSettingsConstants.SingletonId);
        persisted.Should().NotBeNull();
        persisted!.EnablePiiMasking.Should().BeTrue();
        persisted.SecurityMode.Should().Be("Strict");
    }

    [Fact]
    public async Task UpdateSettingsAsync_ShouldSetUpdatedAtUtc()
    {
        var before = DateTime.UtcNow;

        var updated = await _sut.UpdateSettingsAsync(s => s.MaxStorageGb = 100);

        updated.UpdatedAtUtc.Should().BeOnOrAfter(before);
    }

    [Fact]
    public async Task UpdateSettingsAsync_ShouldUpdateCache()
    {
        // Prime cache with defaults
        await _sut.GetSettingsAsync();
        
        // Update
        await _sut.UpdateSettingsAsync(s => s.EnablePiiMasking = true);

        // Verify cache is updated
        var cached = _sut.GetCachedSettings();
        cached!.EnablePiiMasking.Should().BeTrue();
    }

    #endregion

    #region InvalidateCache Tests

    [Fact]
    public async Task InvalidateCache_ShouldClearCachedSettings()
    {
        // Prime cache
        await _sut.GetSettingsAsync();
        _sut.GetCachedSettings().Should().NotBeNull();

        // Invalidate
        _sut.InvalidateCache();

        // Should be null now
        _sut.GetCachedSettings().Should().BeNull();
    }

    #endregion

    #region Convenience Method Tests

    [Fact]
    public async Task IsPiiMaskingEnabledAsync_ShouldReturnCorrectValue()
    {
        await _sut.UpdateSettingsAsync(s => s.EnablePiiMasking = true);

        var result = await _sut.IsPiiMaskingEnabledAsync();

        result.Should().BeTrue();
    }

    [Fact]
    public async Task GetSystemPromptPrefixAsync_WhenSet_ShouldReturnValue()
    {
        await _sut.UpdateSettingsAsync(s => s.SystemPromptPrefix = "Be helpful");

        var result = await _sut.GetSystemPromptPrefixAsync();

        result.Should().Be("Be helpful");
    }

    [Fact]
    public async Task GetSystemPromptPrefixAsync_WhenEmpty_ShouldReturnNull()
    {
        await _sut.UpdateSettingsAsync(s => s.SystemPromptPrefix = "   ");

        var result = await _sut.GetSystemPromptPrefixAsync();

        result.Should().BeNull();
    }

    [Fact]
    public async Task GetSecurityModeAsync_ShouldReturnCurrentMode()
    {
        await _sut.UpdateSettingsAsync(s => s.SecurityMode = "Relaxed");

        var result = await _sut.GetSecurityModeAsync();

        result.Should().Be("Relaxed");
    }

    [Fact]
    public async Task GetExcludedPatternsAsync_WhenSet_ShouldReturnArray()
    {
        await _sut.UpdateSettingsAsync(s => s.ExcludedPatterns = "*.env; *.secret ; .config");

        var result = await _sut.GetExcludedPatternsAsync();

        result.Should().BeEquivalentTo(["*.env", "*.secret", ".config"]);
    }

    [Fact]
    public async Task GetExcludedPatternsAsync_WhenEmpty_ShouldReturnEmptyArray()
    {
        await _sut.UpdateSettingsAsync(s => s.ExcludedPatterns = "");

        var result = await _sut.GetExcludedPatternsAsync();

        result.Should().BeEmpty();
    }

    #endregion

    #region Thread Safety Tests

    [Fact]
    public async Task GetSettingsAsync_WithConcurrentCalls_ShouldNotThrow()
    {
        var tasks = Enumerable.Range(0, 10)
            .Select(_ => _sut.GetSettingsAsync())
            .ToArray();

        var results = await Task.WhenAll(tasks);

        results.Should().AllSatisfy(s => s.Should().NotBeNull());
    }

    [Fact]
    public async Task UpdateSettingsAsync_WithConcurrentCalls_ShouldProcessAll()
    {
        var tasks = Enumerable.Range(0, 5)
            .Select(i => _sut.UpdateSettingsAsync(s => s.MaxStorageGb = i + 1))
            .ToArray();

        await Task.WhenAll(tasks);

        // Final state should be valid (exact value depends on order)
        var final = await _sut.GetSettingsAsync();
        final.MaxStorageGb.Should().BeInRange(1, 5);
    }

    #endregion

    public void Dispose()
    {
        _sut.Dispose();
    }

    /// <summary>
    /// Test implementation of IDbContextFactory for in-memory testing.
    /// </summary>
    private sealed class TestDbContextFactory(DbContextOptions<DocumentAssignmentDbContext> options) 
        : IDbContextFactory<DocumentAssignmentDbContext>
    {
        public DocumentAssignmentDbContext CreateDbContext() => new(options);
    }
}
