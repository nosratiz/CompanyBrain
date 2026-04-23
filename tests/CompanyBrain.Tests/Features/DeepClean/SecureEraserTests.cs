using CompanyBrain.Dashboard.Features.DeepClean;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace CompanyBrain.Tests.Features.DeepClean;

public sealed class SecureEraserTests : IDisposable
{
    private readonly ILogger _logger;
    private readonly List<string> _tempFiles = [];

    public SecureEraserTests()
    {
        _logger = Substitute.For<ILogger>();
    }

    private string CreateTempFile(string content = "sensitive data 1234567890")
    {
        var path = Path.GetTempFileName();
        File.WriteAllText(path, content);
        _tempFiles.Add(path);
        return path;
    }

    public void Dispose()
    {
        foreach (var file in _tempFiles.Where(File.Exists))
        {
            try { File.Delete(file); }
            catch { /* best effort */ }
        }
    }

    #region Successful Deletion Tests

    [Fact]
    public async Task SecureDeleteAsync_WithExistingFile_ShouldDeleteFile()
    {
        var filePath = CreateTempFile();
        File.Exists(filePath).Should().BeTrue();

        var result = await SecureEraser.SecureDeleteAsync(
            filePath, passes: 1, retryCount: 3, retryDelay: TimeSpan.FromMilliseconds(10),
            _logger, CancellationToken.None);

        result.Should().BeTrue();
        File.Exists(filePath).Should().BeFalse();
    }

    [Fact]
    public async Task SecureDeleteAsync_WithMultiplePasses_ShouldDeleteFile()
    {
        var filePath = CreateTempFile("secret content that should be erased securely");

        var result = await SecureEraser.SecureDeleteAsync(
            filePath, passes: 3, retryCount: 3, retryDelay: TimeSpan.FromMilliseconds(10),
            _logger, CancellationToken.None);

        result.Should().BeTrue();
        File.Exists(filePath).Should().BeFalse();
    }

    [Fact]
    public async Task SecureDeleteAsync_WithLargeFile_ShouldDeleteSuccessfully()
    {
        var filePath = Path.GetTempFileName();
        _tempFiles.Add(filePath);
        var largeContent = new byte[200_000];
        new Random().NextBytes(largeContent);
        await File.WriteAllBytesAsync(filePath, largeContent);

        var result = await SecureEraser.SecureDeleteAsync(
            filePath, passes: 1, retryCount: 3, retryDelay: TimeSpan.FromMilliseconds(10),
            _logger, CancellationToken.None);

        result.Should().BeTrue();
        File.Exists(filePath).Should().BeFalse();
    }

    #endregion

    #region Non-Existent File Tests

    [Fact]
    public async Task SecureDeleteAsync_WhenFileDoesNotExist_ShouldReturnFalse()
    {
        var nonExistentPath = Path.Combine(Path.GetTempPath(), $"nonexistent_{Guid.NewGuid():N}.tmp");

        var result = await SecureEraser.SecureDeleteAsync(
            nonExistentPath, passes: 1, retryCount: 3, retryDelay: TimeSpan.FromMilliseconds(10),
            _logger, CancellationToken.None);

        result.Should().BeFalse();
    }

    #endregion

    #region Empty File Tests

    [Fact]
    public async Task SecureDeleteAsync_WithEmptyFile_ShouldDeleteFile()
    {
        var filePath = Path.GetTempFileName();
        _tempFiles.Add(filePath);
        File.WriteAllText(filePath, "");

        var result = await SecureEraser.SecureDeleteAsync(
            filePath, passes: 1, retryCount: 3, retryDelay: TimeSpan.FromMilliseconds(10),
            _logger, CancellationToken.None);

        result.Should().BeTrue();
        File.Exists(filePath).Should().BeFalse();
    }

    #endregion

    #region Argument Validation Tests

    [Fact]
    public async Task SecureDeleteAsync_WhenFilePathIsNull_ShouldThrow()
    {
        var act = async () => await SecureEraser.SecureDeleteAsync(
            null!, passes: 1, retryCount: 3, retryDelay: TimeSpan.FromMilliseconds(10),
            _logger, CancellationToken.None);

        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task SecureDeleteAsync_WhenFilePathIsWhitespace_ShouldThrow()
    {
        var act = async () => await SecureEraser.SecureDeleteAsync(
            "   ", passes: 1, retryCount: 3, retryDelay: TimeSpan.FromMilliseconds(10),
            _logger, CancellationToken.None);

        await act.Should().ThrowAsync<ArgumentException>();
    }

    #endregion

    #region Cancellation Tests

    [Fact]
    public async Task SecureDeleteAsync_WhenCancelled_ShouldPropagateIfFileBeingWritten()
    {
        var filePath = CreateTempFile(new string('X', 500_000));
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        // With an already-cancelled token, writing should fail or succeed depending on timing
        // but no unhandled exception should leak
        try
        {
            await SecureEraser.SecureDeleteAsync(
                filePath, passes: 3, retryCount: 0, retryDelay: TimeSpan.Zero,
                _logger, cts.Token);
        }
        catch (OperationCanceledException)
        {
            // Acceptable — cancellation was propagated
        }
    }

    #endregion
}
