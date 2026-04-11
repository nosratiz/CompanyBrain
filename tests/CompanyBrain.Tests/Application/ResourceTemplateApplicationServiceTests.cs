using CompanyBrain.Application;
using CompanyBrain.Application.Results;
using CompanyBrain.Models;
using CompanyBrain.Services;
using FluentAssertions;
using FluentResults;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace CompanyBrain.Tests.Application;

public sealed class ResourceTemplateApplicationServiceTests : IDisposable
{
    private readonly ResourceTemplateApplicationService _sut;
    private readonly GitRepositoryService _gitService;
    private readonly string _testRoot;

    public ResourceTemplateApplicationServiceTests()
    {
        _testRoot = Path.Combine(Path.GetTempPath(), $"test-templates-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_testRoot);
        
        var logger = Substitute.For<ILogger<GitRepositoryService>>();
        _gitService = new GitRepositoryService(_testRoot, logger);
        
        var appLogger = Substitute.For<ILogger<ResourceTemplateApplicationService>>();
        _sut = new ResourceTemplateApplicationService(_gitService, appLogger);
    }

    #region ListTemplatesAsync Tests

    [Fact]
    public async Task ListTemplatesAsync_WhenNoTemplates_ShouldReturnEmptyList()
    {
        // Act
        var result = await _sut.ListTemplatesAsync(CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeEmpty();
    }

    [Fact]
    public async Task ListTemplatesAsync_WithExistingTemplates_ShouldReturnTemplates()
    {
        // Arrange - create a fake template directory
        var templatesPath = Path.Combine(_testRoot, "templates");
        var templateDir = Path.Combine(templatesPath, "my-template");
        Directory.CreateDirectory(templateDir);
        await File.WriteAllTextAsync(Path.Combine(templateDir, "README.md"), "# Test");

        // Act
        var result = await _sut.ListTemplatesAsync(CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(1);
        result.Value.First().Name.Should().Be("my-template");
    }

    #endregion

    #region GetTemplateFileContentAsync Tests

    [Fact]
    public async Task GetTemplateFileContentAsync_WhenTemplateNotFound_ShouldReturnNotFoundError()
    {
        // Act
        var result = await _sut.GetTemplateFileContentAsync("nonexistent", "file.md", CancellationToken.None);

        // Assert
        result.IsFailed.Should().BeTrue();
        result.Errors.Should().ContainSingle(e => e is NotFoundAppError);
    }

    [Fact]
    public async Task GetTemplateFileContentAsync_WhenFileExists_ShouldReturnContent()
    {
        // Arrange
        var templatesPath = Path.Combine(_testRoot, "templates");
        var templateDir = Path.Combine(templatesPath, "test-template");
        Directory.CreateDirectory(templateDir);
        await File.WriteAllTextAsync(Path.Combine(templateDir, "README.md"), "# Hello World");

        // Act
        var result = await _sut.GetTemplateFileContentAsync("test-template", "README.md", CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be("# Hello World");
    }

    [Fact]
    public async Task GetTemplateFileContentAsync_WhenFileNotFound_ShouldReturnNotFoundError()
    {
        // Arrange
        var templatesPath = Path.Combine(_testRoot, "templates");
        var templateDir = Path.Combine(templatesPath, "test-template");
        Directory.CreateDirectory(templateDir);

        // Act
        var result = await _sut.GetTemplateFileContentAsync("test-template", "missing.md", CancellationToken.None);

        // Assert
        result.IsFailed.Should().BeTrue();
        result.Errors.Should().ContainSingle(e => e is NotFoundAppError);
    }

    [Fact]
    public async Task GetTemplateFileContentAsync_WithNonexistentFile_ShouldReturnError()
    {
        // Arrange - template must exist
        var templatesPath = Path.Combine(_testRoot, "templates");
        var templateDir = Path.Combine(templatesPath, "test-template");
        Directory.CreateDirectory(templateDir);
        await File.WriteAllTextAsync(Path.Combine(templateDir, "readme.md"), "content");

        // Act
        var result = await _sut.GetTemplateFileContentAsync("test-template", "nonexistent.md", CancellationToken.None);

        // Assert
        result.IsFailed.Should().BeTrue();
        result.Errors.Should().ContainSingle(e => e is NotFoundAppError);
    }

    #endregion

    #region DeleteTemplate Tests

    [Fact]
    public void DeleteTemplate_WhenTemplateNotFound_ShouldReturnNotFoundError()
    {
        // Act
        var result = _sut.DeleteTemplate("nonexistent", CancellationToken.None);

        // Assert
        result.IsFailed.Should().BeTrue();
        result.Errors.Should().ContainSingle(e => e is NotFoundAppError);
    }

    [Fact]
    public void DeleteTemplate_WhenTemplateExists_ShouldDeleteSuccessfully()
    {
        // Arrange
        var templatesPath = Path.Combine(_testRoot, "templates");
        var templateDir = Path.Combine(templatesPath, "delete-me");
        Directory.CreateDirectory(templateDir);
        File.WriteAllText(Path.Combine(templateDir, "file.txt"), "content");

        // Act
        var result = _sut.DeleteTemplate("delete-me", CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        Directory.Exists(templateDir).Should().BeFalse();
    }

    #endregion

    #region Cleanup

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_testRoot))
            {
                Directory.Delete(_testRoot, recursive: true);
            }
        }
        catch
        {
            // Ignore cleanup errors
        }
    }

    #endregion
}
