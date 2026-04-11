using CompanyBrain.Services;
using FluentAssertions;

namespace CompanyBrain.Tests.Services;

public sealed class DocumentMarkdownConverterTests : IDisposable
{
    private readonly string _tempDir;

    public DocumentMarkdownConverterTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"doc-converter-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_tempDir))
            {
                Directory.Delete(_tempDir, recursive: true);
            }
        }
        catch
        {
            // Ignore cleanup errors
        }
    }

    #region ConvertAsync - Markdown Files

    [Fact]
    public async Task ConvertAsync_WithMarkdownFile_ShouldReturnAsIs()
    {
        // Arrange
        var filePath = Path.Combine(_tempDir, "test.md");
        var content = "# Hello World\n\nThis is markdown content.";
        await File.WriteAllTextAsync(filePath, content);

        // Act
        var result = await DocumentMarkdownConverter.ConvertAsync(filePath, CancellationToken.None);

        // Assert
        result.Should().Be(content);
    }

    [Fact]
    public async Task ConvertAsync_WithTextFile_ShouldReturnAsIs()
    {
        // Arrange
        var filePath = Path.Combine(_tempDir, "test.txt");
        var content = "Plain text content here.";
        await File.WriteAllTextAsync(filePath, content);

        // Act
        var result = await DocumentMarkdownConverter.ConvertAsync(filePath, CancellationToken.None);

        // Assert
        result.Should().Be(content);
    }

    #endregion

    #region ConvertAsync - HTML Files

    [Fact]
    public async Task ConvertAsync_WithHtmlFile_ShouldConvertToMarkdown()
    {
        // Arrange
        var filePath = Path.Combine(_tempDir, "test.html");
        var html = """
            <html>
            <body>
                <main>
                    <h1>Main Title</h1>
                    <p>First paragraph.</p>
                </main>
            </body>
            </html>
            """;
        await File.WriteAllTextAsync(filePath, html);

        // Act
        var result = await DocumentMarkdownConverter.ConvertAsync(filePath, CancellationToken.None);

        // Assert
        result.Should().Contain("# Main Title");
        result.Should().Contain("First paragraph");
    }

    [Fact]
    public async Task ConvertAsync_WithHtmExtension_ShouldConvertToMarkdown()
    {
        // Arrange
        var filePath = Path.Combine(_tempDir, "test.htm");
        var html = "<html><body><article><h2>Article</h2><p>Content here.</p></article></body></html>";
        await File.WriteAllTextAsync(filePath, html);

        // Act
        var result = await DocumentMarkdownConverter.ConvertAsync(filePath, CancellationToken.None);

        // Assert
        result.Should().Contain("## Article");
    }

    [Fact]
    public async Task ConvertAsync_WithHtmlBoilerplate_ShouldRemove()
    {
        // Arrange
        var filePath = Path.Combine(_tempDir, "test.html");
        var html = """
            <html>
            <head>
                <script>alert('remove');</script>
                <style>.remove { display: none; }</style>
            </head>
            <body>
                <nav>Navigation</nav>
                <header>Header</header>
                <main><p>Keep this content</p></main>
                <footer>Footer</footer>
            </body>
            </html>
            """;
        await File.WriteAllTextAsync(filePath, html);

        // Act
        var result = await DocumentMarkdownConverter.ConvertAsync(filePath, CancellationToken.None);

        // Assert
        result.Should().NotContain("alert");
        result.Should().NotContain(".remove");
        result.Should().Contain("Keep this content");
    }

    #endregion

    #region ConvertAsync - Unsupported Files

    [Fact]
    public async Task ConvertAsync_WithUnsupportedExtension_ShouldThrowNotSupportedException()
    {
        // Arrange
        var filePath = Path.Combine(_tempDir, "test.xyz");
        await File.WriteAllTextAsync(filePath, "content");

        // Act
        var act = () => DocumentMarkdownConverter.ConvertAsync(filePath, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<NotSupportedException>()
            .WithMessage("*Unsupported document type*");
    }

    [Theory]
    [InlineData(".json")]
    [InlineData(".xml")]
    [InlineData(".csv")]
    [InlineData(".yaml")]
    public async Task ConvertAsync_WithOtherUnsupportedTypes_ShouldThrow(string extension)
    {
        // Arrange
        var filePath = Path.Combine(_tempDir, $"test{extension}");
        await File.WriteAllTextAsync(filePath, "content");

        // Act
        var act = () => DocumentMarkdownConverter.ConvertAsync(filePath, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<NotSupportedException>();
    }

    #endregion

    #region ConvertAsync - Cancellation

    [Fact]
    public async Task ConvertAsync_WhenCancelled_ShouldThrowOperationCancelledException()
    {
        // Arrange
        var filePath = Path.Combine(_tempDir, "test.md");
        await File.WriteAllTextAsync(filePath, "content");
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        // Act
        var act = () => DocumentMarkdownConverter.ConvertAsync(filePath, cts.Token);

        // Assert
        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    #endregion

    #region Extension Case Insensitivity Tests

    [Theory]
    [InlineData(".MD")]
    [InlineData(".Md")]
    [InlineData(".md")]
    public async Task ConvertAsync_WithDifferentCaseExtensions_ShouldHandleAllCases(string extension)
    {
        // Arrange
        var filePath = Path.Combine(_tempDir, $"test{extension}");
        await File.WriteAllTextAsync(filePath, "# Test");

        // Act
        var result = await DocumentMarkdownConverter.ConvertAsync(filePath, CancellationToken.None);

        // Assert
        result.Should().Be("# Test");
    }

    [Theory]
    [InlineData(".HTML")]
    [InlineData(".Html")]
    [InlineData(".html")]
    public async Task ConvertAsync_WithHtmlCaseVariations_ShouldConvert(string extension)
    {
        // Arrange
        var filePath = Path.Combine(_tempDir, $"test{extension}");
        await File.WriteAllTextAsync(filePath, "<body><p>Text</p></body>");

        // Act
        var result = await DocumentMarkdownConverter.ConvertAsync(filePath, CancellationToken.None);

        // Assert
        result.Should().Contain("Text");
    }

    #endregion
}
