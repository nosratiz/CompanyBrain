using CompanyBrain.Dashboard.Data;
using CompanyBrain.Dashboard.Data.Models;
using CompanyBrain.Dashboard.Mcp;
using CompanyBrain.Dashboard.Services;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Protocol;
using NSubstitute;

namespace CompanyBrain.Tests.Mcp;

public sealed class GovernanceToolWrapperTests : IDisposable
{
    private readonly DbContextOptions<DocumentAssignmentDbContext> _dbOptions;
    private readonly SettingsService _settingsService;
    private readonly GovernanceToolWrapper _sut;

    public GovernanceToolWrapperTests()
    {
        _dbOptions = new DbContextOptionsBuilder<DocumentAssignmentDbContext>()
            .UseInMemoryDatabase($"ToolWrapperTests_{Guid.NewGuid():N}")
            .Options;
        
        var contextFactory = new TestDbContextFactory(_dbOptions);
        var settingsLogger = Substitute.For<ILogger<SettingsService>>();
        _settingsService = new SettingsService(contextFactory, settingsLogger);
        
        var wrapperLogger = Substitute.For<ILogger<GovernanceToolWrapper>>();
        _sut = new GovernanceToolWrapper(_settingsService, wrapperLogger);
    }

    public void Dispose() => _settingsService.Dispose();

    #region FilterResultAsync Tests

    [Fact]
    public async Task FilterResultAsync_WhenPiiMaskingDisabled_ShouldReturnOriginal()
    {
        // Arrange - default has PII masking disabled
        var input = new CallToolResult
        {
            Content = [new TextContentBlock { Text = "Email: user@test.com" }],
            IsError = false
        };

        // Act
        var result = await _sut.FilterResultAsync(input);

        // Assert
        var text = result.Content.OfType<TextContentBlock>().Single();
        text.Text.Should().Be("Email: user@test.com");
    }

    [Fact]
    public async Task FilterResultAsync_WhenPiiMaskingEnabled_ShouldRedactPii()
    {
        // Arrange
        await _settingsService.UpdateSettingsAsync(s => s.EnablePiiMasking = true);

        var input = new CallToolResult
        {
            Content = [new TextContentBlock { Text = "API Key: sk-1234567890abcdefghij" }],
            IsError = false
        };

        // Act
        var result = await _sut.FilterResultAsync(input);

        // Assert
        var text = result.Content.OfType<TextContentBlock>().Single();
        text.Text.Should().Be("API Key: [API_KEY_REDACTED]");
    }

    [Fact]
    public async Task FilterResultAsync_WithCachedSettings_ShouldUseCacheFirst()
    {
        // Arrange - prime the cache
        await _settingsService.UpdateSettingsAsync(s => s.EnablePiiMasking = true);
        
        // Force cache to be populated
        await _settingsService.GetSettingsAsync();

        var input = new CallToolResult
        {
            Content = [new TextContentBlock { Text = "Email: user@test.com" }],
            IsError = false
        };

        // Act
        var result = await _sut.FilterResultAsync(input);

        // Assert - should have used cached settings with PII masking
        var text = result.Content.OfType<TextContentBlock>().Single();
        text.Text.Should().Be("Email: [EMAIL_REDACTED]");
    }

    [Fact]
    public async Task FilterResultAsync_ShouldPreserveIsError()
    {
        // Arrange
        await _settingsService.UpdateSettingsAsync(s => s.EnablePiiMasking = true);

        var input = new CallToolResult
        {
            Content = [new TextContentBlock { Text = "Error" }],
            IsError = true
        };

        // Act
        var result = await _sut.FilterResultAsync(input);

        // Assert
        result.IsError.Should().BeTrue();
    }

    [Fact]
    public async Task FilterResultAsync_ShouldNotModifyCleanText()
    {
        // Arrange
        await _settingsService.UpdateSettingsAsync(s => s.EnablePiiMasking = true);

        var input = new CallToolResult
        {
            Content = [new TextContentBlock { Text = "No sensitive data here" }],
            IsError = false
        };

        // Act
        var result = await _sut.FilterResultAsync(input);

        // Assert
        var text = result.Content.OfType<TextContentBlock>().Single();
        text.Text.Should().Be("No sensitive data here");
    }

    #endregion

    #region ValidatePathAsync Tests

    [Fact]
    public async Task ValidatePathAsync_InRelaxedMode_ShouldAlwaysSucceed()
    {
        // Arrange
        await _settingsService.UpdateSettingsAsync(s => s.SecurityMode = "Relaxed");

        // Act
        var (isValid, errorResult) = await _sut.ValidatePathAsync("../../../escape", "/base");

        // Assert
        isValid.Should().BeTrue();
        errorResult.Should().BeNull();
    }

    [Fact]
    public async Task ValidatePathAsync_WithSafePath_ShouldSucceed()
    {
        // Arrange - default mode is "Moderate"
        
        // Act
        var (isValid, errorResult) = await _sut.ValidatePathAsync("docs/file.md", "/base/tenant");

        // Assert
        isValid.Should().BeTrue();
        errorResult.Should().BeNull();
    }

    [Fact]
    public async Task ValidatePathAsync_WithDirectoryTraversal_ShouldReturnSecurityError()
    {
        // Arrange
        await _settingsService.UpdateSettingsAsync(s => s.SecurityMode = "Strict");

        // Act
        var (isValid, errorResult) = await _sut.ValidatePathAsync("../../etc/passwd", "/base/tenant");

        // Assert
        isValid.Should().BeFalse();
        errorResult.Should().NotBeNull();
        errorResult!.IsError.Should().BeTrue();
        
        var text = errorResult.Content.OfType<TextContentBlock>().Single();
        text.Text.Should().Contain("SECURITY ERROR");
        text.Text.Should().Contain("Directory traversal");
    }

    [Fact]
    public async Task ValidatePathAsync_WithExcludedPattern_ShouldReturnSecurityError()
    {
        // Arrange
        await _settingsService.UpdateSettingsAsync(s => s.ExcludedPatterns = "*.secret");

        // Act
        var (isValid, errorResult) = await _sut.ValidatePathAsync("creds.secret", "/base/tenant");

        // Assert
        isValid.Should().BeFalse();
        errorResult.Should().NotBeNull();
        
        var text = errorResult!.Content.OfType<TextContentBlock>().Single();
        text.Text.Should().Contain("excluded pattern");
    }

    #endregion

    #region CreateFilteredTextResultAsync Tests

    [Fact]
    public async Task CreateFilteredTextResultAsync_WhenPiiMaskingDisabled_ShouldReturnOriginal()
    {
        // Arrange - default has PII masking disabled

        // Act
        var result = await _sut.CreateFilteredTextResultAsync("Email: user@test.com");

        // Assert
        result.IsError.Should().BeFalse();
        var text = result.Content.OfType<TextContentBlock>().Single();
        text.Text.Should().Be("Email: user@test.com");
    }

    [Fact]
    public async Task CreateFilteredTextResultAsync_WhenPiiMaskingEnabled_ShouldRedact()
    {
        // Arrange
        await _settingsService.UpdateSettingsAsync(s => s.EnablePiiMasking = true);

        // Act
        var result = await _sut.CreateFilteredTextResultAsync("IP: 192.168.1.1");

        // Assert
        result.IsError.Should().BeFalse();
        var text = result.Content.OfType<TextContentBlock>().Single();
        text.Text.Should().Be("IP: [IP_REDACTED]");
    }

    [Fact]
    public async Task CreateFilteredTextResultAsync_WithMultiplePii_ShouldRedactAll()
    {
        // Arrange
        await _settingsService.UpdateSettingsAsync(s => s.EnablePiiMasking = true);

        // Act
        var result = await _sut.CreateFilteredTextResultAsync(
            "Contact: admin@corp.com at 10.0.0.1 with key AKIAIOSFODNN7EXAMPLE");

        // Assert
        var text = result.Content.OfType<TextContentBlock>().Single();
        text.Text.Should().Contain("[EMAIL_REDACTED]");
        text.Text.Should().Contain("[IP_REDACTED]");
        text.Text.Should().Contain("[AWS_KEY_REDACTED]");
    }

    #endregion

    private sealed class TestDbContextFactory(DbContextOptions<DocumentAssignmentDbContext> options) 
        : IDbContextFactory<DocumentAssignmentDbContext>
    {
        public DocumentAssignmentDbContext CreateDbContext() => new(options);
    }
}
