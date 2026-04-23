using CompanyBrain.Search.Vector;

namespace CompanyBrain.Tests.Search.Vector;

public sealed class EmbeddingProviderDefaultsTests
{
    [Fact]
    public void OpenAI_defaults_apply_when_unset()
    {
        var (model, dim) = EmbeddingProviderDefaults.Resolve(EmbeddingProviderType.OpenAI, "", 0);
        Assert.Equal("text-embedding-3-small", model);
        Assert.Equal(1536, dim);
    }

    [Fact]
    public void Gemini_defaults_apply_when_unset()
    {
        var (model, dim) = EmbeddingProviderDefaults.Resolve(EmbeddingProviderType.Gemini, "", 0);
        Assert.Equal("text-embedding-004", model);
        Assert.Equal(768, dim);
    }

    [Fact]
    public void Voyage_defaults_apply_when_unset()
    {
        var (model, dim) = EmbeddingProviderDefaults.Resolve(EmbeddingProviderType.Voyage, "", 0);
        Assert.Equal("voyage-3", model);
        Assert.Equal(1024, dim);
    }

    [Fact]
    public void Configured_values_take_precedence()
    {
        var (model, dim) = EmbeddingProviderDefaults.Resolve(
            EmbeddingProviderType.OpenAI, "text-embedding-3-large", 3072);
        Assert.Equal("text-embedding-3-large", model);
        Assert.Equal(3072, dim);
    }
}
