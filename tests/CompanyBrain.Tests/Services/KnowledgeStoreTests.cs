using CompanyBrain.Constants;
using CompanyBrain.Services;
using FluentAssertions;

namespace CompanyBrain.Tests.Services;

public sealed class KnowledgeStoreTests : IDisposable
{
    private readonly string tempDirectory = Path.Combine(Path.GetTempPath(), $"company-brain-tests-{Guid.NewGuid():N}");

    [Fact]
    public async Task SaveMarkdownAsync_ThenReadResourceAsync_ShouldRoundTripContent()
    {
        var store = new KnowledgeStore(tempDirectory);

        var saved = await store.SaveMarkdownAsync("Architecture Notes", "# Title\n\nBody", CancellationToken.None);
        var resource = await store.ReadResourceAsync(saved.ResourceUri, CancellationToken.None);

        resource.IsSuccess.Should().BeTrue();
        resource.Value.FileName.Should().Be("architecture-notes.md");
        resource.Value.Content.Should().Be("# Title\n\nBody");
    }

    [Fact]
    public async Task SearchAsync_WhenQueryIsEmpty_ShouldFailValidation()
    {
        var store = new KnowledgeStore(tempDirectory);

        var result = await store.SearchAsync("   ", 5, CancellationToken.None);

        result.IsFailed.Should().BeTrue();
        result.Errors.Should().ContainSingle(error => error.Message == "A non-empty query is required.");
    }

    [Fact]
    public async Task SearchAsync_WhenMatchingDocumentExists_ShouldReturnFormattedResult()
    {
        var store = new KnowledgeStore(tempDirectory);
        await store.SaveMarkdownAsync("Architecture Notes", "Event driven systems and sagas are core patterns.", CancellationToken.None);

        var result = await store.SearchAsync("sagas", 5, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Contain("Search results for 'sagas':");
        result.Value.Should().Contain($"({CompanyBrainConstants.ResourceScheme}architecture-notes.md)");
    }

    public void Dispose()
    {
        if (Directory.Exists(tempDirectory))
        {
            Directory.Delete(tempDirectory, recursive: true);
        }
    }
}