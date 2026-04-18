using CompanyBrain.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace CompanyBrain.Tests.Services;

public sealed class GitRepositoryServiceTests : IDisposable
{
    private readonly GitRepositoryService _sut;
    private readonly string _testRoot;

    public GitRepositoryServiceTests()
    {
        _testRoot = Path.Combine(Path.GetTempPath(), $"git-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_testRoot);
        
        var logger = Substitute.For<ILogger<GitRepositoryService>>();
        _sut = new GitRepositoryService(_testRoot, logger);
    }

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

    #region CloneRepositoryAsync Validation Tests

    [Fact]
    public async Task CloneRepositoryAsync_WithNullUrl_ShouldThrowArgumentException()
    {
        // Act
        var act = () => _sut.CloneRepositoryAsync(null!, "template", null, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task CloneRepositoryAsync_WithEmptyUrl_ShouldThrowArgumentException()
    {
        // Act
        var act = () => _sut.CloneRepositoryAsync("  ", "template", null, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task CloneRepositoryAsync_WithNullTemplateName_ShouldThrowArgumentException()
    {
        // Act
        var act = () => _sut.CloneRepositoryAsync("https://github.com/test/repo", null!, null, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task CloneRepositoryAsync_WithEmptyTemplateName_ShouldThrowArgumentException()
    {
        // Act
        var act = () => _sut.CloneRepositoryAsync("https://github.com/test/repo", "  ", null, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>();
    }

    #endregion

    #region ListTemplatesAsync Tests

    [Fact]
    public async Task ListTemplatesAsync_WhenTemplatesDirectoryDoesNotExist_ShouldReturnEmptyList()
    {
        // Act
        var result = await _sut.ListTemplatesAsync(CancellationToken.None);

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task ListTemplatesAsync_WithTemplates_ShouldReturnAllTemplates()
    {
        // Arrange
        var templatesPath = Path.Combine(_testRoot, "templates");
        Directory.CreateDirectory(Path.Combine(templatesPath, "template-a"));
        Directory.CreateDirectory(Path.Combine(templatesPath, "template-b"));
        await File.WriteAllTextAsync(Path.Combine(templatesPath, "template-a", "readme.md"), "# A");
        await File.WriteAllTextAsync(Path.Combine(templatesPath, "template-b", "readme.md"), "# B");

        // Act
        var result = await _sut.ListTemplatesAsync(CancellationToken.None);

        // Assert
        result.Should().HaveCount(2);
        result.Select(t => t.Name).Should().Contain("template-a");
        result.Select(t => t.Name).Should().Contain("template-b");
    }

    [Fact]
    public async Task ListTemplatesAsync_WhenCancelled_ShouldThrowOperationCancelledException()
    {
        // Arrange
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        // Act
        var act = () => _sut.ListTemplatesAsync(cts.Token);

        // Assert
        await act.Should().ThrowAsync<OperationCanceledException>();
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
        result.Errors.First().Message.Should().Contain("not found");
    }

    [Fact]
    public async Task GetTemplateFileContentAsync_WhenFileNotFound_ShouldReturnNotFoundError()
    {
        // Arrange
        var templatesPath = Path.Combine(_testRoot, "templates", "test");
        Directory.CreateDirectory(templatesPath);

        // Act
        var result = await _sut.GetTemplateFileContentAsync("test", "missing.md", CancellationToken.None);

        // Assert
        result.IsFailed.Should().BeTrue();
        result.Errors.First().Message.Should().Contain("not found");
    }

    [Fact]
    public async Task GetTemplateFileContentAsync_WhenFileExists_ShouldReturnContent()
    {
        // Arrange
        var templatesPath = Path.Combine(_testRoot, "templates", "test");
        Directory.CreateDirectory(templatesPath);
        var content = "# Test Content\n\nSome markdown here.";
        await File.WriteAllTextAsync(Path.Combine(templatesPath, "README.md"), content);

        // Act
        var result = await _sut.GetTemplateFileContentAsync("test", "README.md", CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(content);
    }

    [Fact]
    public async Task GetTemplateFileContentAsync_WithNonexistentFile_ShouldReturnNotFoundError()
    {
        // Arrange
        var templatesPath = Path.Combine(_testRoot, "templates", "test");
        Directory.CreateDirectory(templatesPath);

        // Act
        var result = await _sut.GetTemplateFileContentAsync("test", "nonexistent.md", CancellationToken.None);

        // Assert
        result.IsFailed.Should().BeTrue();
        result.Errors.First().Message.Should().Contain("not found");
    }

    [Fact]
    public async Task GetTemplateFileContentAsync_WithNestedPath_ShouldReturnContent()
    {
        // Arrange
        var templatesPath = Path.Combine(_testRoot, "templates", "test", "docs", "api");
        Directory.CreateDirectory(templatesPath);
        await File.WriteAllTextAsync(Path.Combine(templatesPath, "guide.md"), "Nested content");

        // Act
        var result = await _sut.GetTemplateFileContentAsync("test", "docs/api/guide.md", CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be("Nested content");
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
        result.Errors.First().Message.Should().Contain("not found");
    }

    [Fact]
    public void DeleteTemplate_WhenTemplateExists_ShouldDeleteSuccessfully()
    {
        // Arrange
        var templatePath = Path.Combine(_testRoot, "templates", "to-delete");
        Directory.CreateDirectory(templatePath);
        File.WriteAllText(Path.Combine(templatePath, "file.txt"), "content");

        // Act
        var result = _sut.DeleteTemplate("to-delete", CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        Directory.Exists(templatePath).Should().BeFalse();
    }

    [Fact]
    public void DeleteTemplate_WhenCancelled_ShouldThrowOperationCancelledException()
    {
        // Arrange
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act
        var act = () => _sut.DeleteTemplate("any", cts.Token);

        // Assert
        act.Should().Throw<OperationCanceledException>();
    }

    #endregion
}
