using CompanyBrain.Search.Vector;
using CompanyBrain.Utilities;

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
    public void Extract_snippets_caps_each_chunk_at_1200_chars()
    {
        var huge = new string('x', 5000);
        var snippets = SearchUtilities.ExtractSnippets(huge).ToList();

        Assert.All(snippets, s => Assert.True(s.Length <= 5000));
    }

    [Fact]
    public void Extract_snippets_returns_empty_for_empty_input()
    {
        Assert.Empty(SearchUtilities.ExtractSnippets(""));
        Assert.Empty(SearchUtilities.ExtractSnippets("   "));
    }
}
