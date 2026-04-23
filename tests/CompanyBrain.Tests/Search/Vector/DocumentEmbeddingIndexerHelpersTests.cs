using CompanyBrain.Search.Vector;

namespace CompanyBrain.Tests.Search.Vector;

public sealed class DocumentEmbeddingIndexerHelpersTests
{
    [Fact]
    public void Compute_hash_is_deterministic_and_content_sensitive()
    {
        Assert.Equal(
            DocumentEmbeddingIndexer.ComputeHash("hello"),
            DocumentEmbeddingIndexer.ComputeHash("hello"));

        Assert.NotEqual(
            DocumentEmbeddingIndexer.ComputeHash("hello"),
            DocumentEmbeddingIndexer.ComputeHash("hello!"));
    }

    [Fact]
    public void Build_redacted_snippet_caps_length()
    {
        var huge = new string('x', 5000);
        var snippet = DocumentEmbeddingIndexer.BuildRedactedSnippet(huge);

        Assert.True(snippet.Length <= 1200);
    }

    [Fact]
    public void Build_redacted_snippet_returns_empty_for_empty_input()
    {
        Assert.Equal(string.Empty, DocumentEmbeddingIndexer.BuildRedactedSnippet(""));
        Assert.Equal(string.Empty, DocumentEmbeddingIndexer.BuildRedactedSnippet("   "));
    }
}
