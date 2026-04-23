using CompanyBrain.Dashboard.Data;
using CompanyBrain.Dashboard.Data.Models;
using CompanyBrain.Dashboard.Mcp;
using CompanyBrain.Dashboard.Services;
using CompanyBrain.Dashboard.Services.Audit;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Protocol;
using NSubstitute;

namespace CompanyBrain.Tests.Mcp;

public sealed class McpGovernanceFilterTests : IDisposable
{
    private readonly DbContextOptions<DocumentAssignmentDbContext> _dbOptions;
    private readonly SettingsService _settingsService;
    private readonly McpGovernanceFilter _sut;

    public McpGovernanceFilterTests()
    {
        _dbOptions = new DbContextOptionsBuilder<DocumentAssignmentDbContext>()
            .UseInMemoryDatabase($"GovernanceFilterTests_{Guid.NewGuid():N}")
            .Options;
        
        var contextFactory = new TestDbContextFactory(_dbOptions);
        var settingsLogger = Substitute.For<ILogger<SettingsService>>();
        var audit = Substitute.For<IAuditService>();
        _settingsService = new SettingsService(contextFactory, audit, settingsLogger);
        
        var filterLogger = Substitute.For<ILogger<McpGovernanceFilter>>();
        _sut = new McpGovernanceFilter(_settingsService, filterLogger);
    }

    public void Dispose() => _settingsService.Dispose();

    #region FilterToolOutputAsync Tests

    [Fact]
    public async Task FilterToolOutputAsync_WhenPiiMaskingDisabled_ShouldReturnUnmodified()
    {
        // Arrange - default settings have PII masking disabled
        var input = new CallToolResult
        {
            Content = [new TextContentBlock { Text = "Contact: user@example.com" }],
            IsError = false
        };

        // Act
        var result = await _sut.FilterToolOutputAsync(input);

        // Assert
        result.Content.Should().HaveCount(1);
        var text = result.Content.OfType<TextContentBlock>().Single();
        text.Text.Should().Be("Contact: user@example.com");
    }

    [Fact]
    public async Task FilterToolOutputAsync_WhenPiiMaskingEnabled_ShouldRedactEmail()
    {
        // Arrange
        await _settingsService.UpdateSettingsAsync(s => s.EnablePiiMasking = true);
        
        var input = new CallToolResult
        {
            Content = [new TextContentBlock { Text = "Contact: user@example.com" }],
            IsError = false
        };

        // Act
        var result = await _sut.FilterToolOutputAsync(input);

        // Assert
        result.Content.Should().HaveCount(1);
        var text = result.Content.OfType<TextContentBlock>().Single();
        text.Text.Should().Be("Contact: [EMAIL_REDACTED]");
    }

    [Fact]
    public async Task FilterToolOutputAsync_WithMultiplePiiTypes_ShouldRedactAll()
    {
        // Arrange
        await _settingsService.UpdateSettingsAsync(s => s.EnablePiiMasking = true);
        
        var input = new CallToolResult
        {
            Content = [new TextContentBlock { Text = "Email: test@site.org, IP: 192.168.1.1, Key: sk-1234567890abcdefghij" }],
            IsError = false
        };

        // Act
        var result = await _sut.FilterToolOutputAsync(input);

        // Assert
        var text = result.Content.OfType<TextContentBlock>().Single();
        text.Text.Should().Contain("[EMAIL_REDACTED]");
        text.Text.Should().Contain("[IP_REDACTED]");
        text.Text.Should().Contain("[API_KEY_REDACTED]");
    }

    [Fact]
    public async Task FilterToolOutputAsync_ShouldPreserveIsError()
    {
        // Arrange
        await _settingsService.UpdateSettingsAsync(s => s.EnablePiiMasking = true);
        
        var input = new CallToolResult
        {
            Content = [new TextContentBlock { Text = "Error occurred" }],
            IsError = true
        };

        // Act
        var result = await _sut.FilterToolOutputAsync(input);

        // Assert
        result.IsError.Should().BeTrue();
    }

    #endregion

    #region ApplySystemPromptAsync Tests

    [Fact]
    public async Task ApplySystemPromptAsync_WhenNoPrefixAndNoPiiMasking_ShouldReturnUnmodified()
    {
        // Arrange - defaults have empty prefix and disabled PII masking
        var input = new ReadResourceResult
        {
            Contents = [new TextResourceContents { Uri = "file:///test.md", Text = "Original content" }]
        };

        // Act
        var result = await _sut.ApplySystemPromptAsync(input);

        // Assert
        var text = result.Contents.OfType<TextResourceContents>().Single();
        text.Text.Should().Be("Original content");
    }

    [Fact]
    public async Task ApplySystemPromptAsync_WithPrefix_ShouldPrependToContent()
    {
        // Arrange
        await _settingsService.UpdateSettingsAsync(s => s.SystemPromptPrefix = "GOVERNANCE: Be careful");
        
        var input = new ReadResourceResult
        {
            Contents = [new TextResourceContents { Uri = "file:///test.md", Text = "Original content" }]
        };

        // Act
        var result = await _sut.ApplySystemPromptAsync(input);

        // Assert
        var text = result.Contents.OfType<TextResourceContents>().Single();
        text.Text.Should().StartWith("GOVERNANCE: Be careful");
        text.Text.Should().Contain("Original content");
    }

    [Fact]
    public async Task ApplySystemPromptAsync_WithPrefixAndPiiMasking_ShouldApplyBoth()
    {
        // Arrange
        await _settingsService.UpdateSettingsAsync(s =>
        {
            s.SystemPromptPrefix = "PREFIX:";
            s.EnablePiiMasking = true;
        });
        
        var input = new ReadResourceResult
        {
            Contents = [new TextResourceContents { Uri = "file:///test.md", Text = "Contact: user@example.com" }]
        };

        // Act
        var result = await _sut.ApplySystemPromptAsync(input);

        // Assert
        var text = result.Contents.OfType<TextResourceContents>().Single();
        text.Text.Should().StartWith("PREFIX:");
        text.Text.Should().Contain("[EMAIL_REDACTED]");
        text.Text.Should().NotContain("user@example.com");
    }

    [Fact]
    public async Task ApplySystemPromptAsync_NoPrefixButPiiMasking_ShouldOnlyRedact()
    {
        // Arrange
        await _settingsService.UpdateSettingsAsync(s =>
        {
            s.SystemPromptPrefix = "   ";
            s.EnablePiiMasking = true;
        });
        
        var input = new ReadResourceResult
        {
            Contents = [new TextResourceContents { Uri = "file:///test.md", Text = "Key: sk-1234567890abcdefghij" }]
        };

        // Act
        var result = await _sut.ApplySystemPromptAsync(input);

        // Assert
        var text = result.Contents.OfType<TextResourceContents>().Single();
        text.Text.Should().Be("Key: [API_KEY_REDACTED]");
    }

    #endregion

    #region ValidatePathAsync Tests

    [Fact]
    public async Task ValidatePathAsync_InRelaxedMode_ShouldAlwaysPass()
    {
        // Arrange
        await _settingsService.UpdateSettingsAsync(s => s.SecurityMode = "Relaxed");

        // Act - even a dangerous path should pass in relaxed mode
        var (isValid, error) = await _sut.ValidatePathAsync("../../../etc/passwd", "/base");

        // Assert
        isValid.Should().BeTrue();
        error.Should().BeNull();
    }

    [Fact]
    public async Task ValidatePathAsync_WithSafePath_ShouldPass()
    {
        // Arrange - default mode is "Moderate"
        var (isValid, error) = await _sut.ValidatePathAsync("docs/readme.md", "/base/tenant");

        // Assert
        isValid.Should().BeTrue();
        error.Should().BeNull();
    }

    [Fact]
    public async Task ValidatePathAsync_WithDirectoryTraversal_ShouldFail()
    {
        // Arrange
        await _settingsService.UpdateSettingsAsync(s => s.SecurityMode = "Strict");

        // Act
        var (isValid, error) = await _sut.ValidatePathAsync("../../../etc/passwd", "/base/tenant");

        // Assert
        isValid.Should().BeFalse();
        error.Should().Contain("directory");
    }

    [Fact]
    public async Task ValidatePathAsync_WithExcludedPattern_ShouldFail()
    {
        // Arrange
        await _settingsService.UpdateSettingsAsync(s => s.ExcludedPatterns = "*.env;*.secret");

        // Act
        var (isValid, error) = await _sut.ValidatePathAsync("config.env", "/base/tenant");

        // Assert
        isValid.Should().BeFalse();
        error.Should().Contain("excluded pattern");
    }

    #endregion

    #region IsStrictModeAsync Tests

    [Fact]
    public async Task IsStrictModeAsync_WhenStrict_ShouldReturnTrue()
    {
        // Arrange
        await _settingsService.UpdateSettingsAsync(s => s.SecurityMode = "Strict");

        // Act
        var result = await _sut.IsStrictModeAsync();

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task IsStrictModeAsync_WhenModerate_ShouldReturnFalse()
    {
        // Arrange - default is Moderate
        // Act
        var result = await _sut.IsStrictModeAsync();

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task IsStrictModeAsync_WhenRelaxed_ShouldReturnFalse()
    {
        // Arrange
        await _settingsService.UpdateSettingsAsync(s => s.SecurityMode = "Relaxed");

        // Act
        var result = await _sut.IsStrictModeAsync();

        // Assert
        result.Should().BeFalse();
    }

    #endregion

    private sealed class TestDbContextFactory(DbContextOptions<DocumentAssignmentDbContext> options) 
        : IDbContextFactory<DocumentAssignmentDbContext>
    {
        public DocumentAssignmentDbContext CreateDbContext() => new(options);
    }
}
