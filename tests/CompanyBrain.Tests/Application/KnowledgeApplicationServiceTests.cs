using CompanyBrain.Application;
using CompanyBrain.Services;
using FluentAssertions;

namespace CompanyBrain.Tests.Application;

public sealed class KnowledgeApplicationServiceTests : IDisposable
{
    private readonly string tempDirectory = Path.Combine(Path.GetTempPath(), $"company-brain-app-tests-{Guid.NewGuid():N}");

    [Fact]
    public async Task IngestDocumentFromPathAsync_WhenFileDoesNotExist_ShouldReturnNotFoundResult()
    {
        var store = new KnowledgeStore(tempDirectory);
        using var httpClient = new HttpClient();
        var wikiIngester = new WikiIngester(httpClient);
        var service = new KnowledgeApplicationService(store, wikiIngester);

        var result = await service.IngestDocumentFromPathAsync("missing-file.docx", "missing-file", CancellationToken.None);

        result.IsFailed.Should().BeTrue();
        result.Errors.Should().ContainSingle(error => error.Message.StartsWith("Document not found:"));
    }

    public void Dispose()
    {
        if (Directory.Exists(tempDirectory))
        {
            Directory.Delete(tempDirectory, recursive: true);
        }
    }
}