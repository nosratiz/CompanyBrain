using CompanyBrain.Dashboard.Data;
using CompanyBrain.Dashboard.Data.Models;
using CompanyBrain.Dashboard.Features.SharePoint.Models;
using CompanyBrain.Dashboard.Features.SharePoint.Services;
using CompanyBrain.Dashboard.Services;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace CompanyBrain.Tests.Features.SharePoint;

public sealed class SharePointSettingsProviderTests : IDisposable
{
    private readonly DbContextOptions<DocumentAssignmentDbContext> _dbOptions;
    private readonly SettingsService _settingsService;
    private readonly ILogger<SharePointSettingsProvider> _logger;
    private readonly SharePointSyncOptions _fallback;

    public SharePointSettingsProviderTests()
    {
        _dbOptions = new DbContextOptionsBuilder<DocumentAssignmentDbContext>()
            .UseInMemoryDatabase($"SPSettingsTests_{Guid.NewGuid():N}")
            .Options;

        var contextFactory = new TestDbContextFactory(_dbOptions);
        _settingsService = new SettingsService(contextFactory, Substitute.For<ILogger<SettingsService>>());
        _logger = Substitute.For<ILogger<SharePointSettingsProvider>>();

        _fallback = new SharePointSyncOptions
        {
            ClientId = "fallback-client-id",
            TenantId = "fallback-tenant-id",
            SyncIntervalMinutes = 60
        };
    }

    public void Dispose() => _settingsService.Dispose();

    private SharePointSettingsProvider CreateSut() =>
        new(_settingsService, Options.Create(_fallback), _logger);

    #region GetEffectiveOptionsAsync Tests

    [Fact]
    public async Task GetEffectiveOptionsAsync_WhenSyncDisabled_ShouldReturnFallback()
    {
        // SharePointSyncEnabled defaults to false
        var sut = CreateSut();

        var result = await sut.GetEffectiveOptionsAsync();

        result.ClientId.Should().Be("fallback-client-id");
        result.TenantId.Should().Be("fallback-tenant-id");
    }

    [Fact]
    public async Task GetEffectiveOptionsAsync_WhenSyncEnabledWithDbSettings_ShouldReturnDbSettings()
    {
        await _settingsService.UpdateSettingsAsync(s =>
        {
            s.SharePointSyncEnabled = true;
            s.SharePointClientId = "db-client-id";
            s.SharePointTenantId = "db-tenant-id";
            s.SharePointClientSecret = "db-secret";
            s.SharePointSyncIntervalMinutes = 15;
        });

        var sut = CreateSut();
        var result = await sut.GetEffectiveOptionsAsync();

        result.ClientId.Should().Be("db-client-id");
        result.TenantId.Should().Be("db-tenant-id");
        result.ClientSecret.Should().Be("db-secret");
        result.SyncIntervalMinutes.Should().Be(15);
    }

    [Fact]
    public async Task GetEffectiveOptionsAsync_WhenSyncEnabledButNoDbClientId_ShouldReturnFallback()
    {
        await _settingsService.UpdateSettingsAsync(s =>
        {
            s.SharePointSyncEnabled = true;
            s.SharePointClientId = "";
            s.SharePointTenantId = "";
        });

        var sut = CreateSut();
        var result = await sut.GetEffectiveOptionsAsync();

        result.ClientId.Should().Be("fallback-client-id");
    }

    [Fact]
    public async Task GetEffectiveOptionsAsync_WhenDbSyncIntervalIsZero_ShouldUseFallbackInterval()
    {
        await _settingsService.UpdateSettingsAsync(s =>
        {
            s.SharePointSyncEnabled = true;
            s.SharePointClientId = "client-id";
            s.SharePointTenantId = "tenant-id";
            s.SharePointSyncIntervalMinutes = 0;
        });

        var sut = CreateSut();
        var result = await sut.GetEffectiveOptionsAsync();

        result.SyncIntervalMinutes.Should().Be(_fallback.SyncIntervalMinutes);
    }

    #endregion

    #region IsConfiguredAsync Tests

    [Fact]
    public async Task IsConfiguredAsync_WhenNotConfigured_ShouldReturnFalse()
    {
        var emptyFallback = new SharePointSyncOptions { ClientId = "", TenantId = "" };
        var sut = new SharePointSettingsProvider(_settingsService, Options.Create(emptyFallback), _logger);

        var result = await sut.IsConfiguredAsync();

        result.Should().BeFalse();
    }

    [Fact]
    public async Task IsConfiguredAsync_WhenFallbackHasPlaceholders_ShouldReturnFalse()
    {
        var fallbackWithPlaceholders = new SharePointSyncOptions
        {
            ClientId = "YOUR_AZURE_AD_CLIENT_ID",
            TenantId = "YOUR_AZURE_AD_TENANT_ID"
        };
        var sut = new SharePointSettingsProvider(
            _settingsService,
            Options.Create(fallbackWithPlaceholders),
            _logger);

        var result = await sut.IsConfiguredAsync();

        result.Should().BeFalse();
    }

    [Fact]
    public async Task IsConfiguredAsync_WhenValidFallbackConfigured_ShouldReturnTrue()
    {
        // The default fallback has "fallback-client-id" which is valid (not empty, not placeholder)
        var sut = CreateSut();

        var result = await sut.IsConfiguredAsync();

        result.Should().BeTrue();
    }

    #endregion

    private sealed class TestDbContextFactory(DbContextOptions<DocumentAssignmentDbContext> options)
        : IDbContextFactory<DocumentAssignmentDbContext>
    {
        public DocumentAssignmentDbContext CreateDbContext() => new(options);
    }
}
