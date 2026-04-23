using CompanyBrain.Search.Vector;

namespace CompanyBrain.Tests.Search.Vector;

public sealed class EmbeddingOptionsSignatureTests
{
    [Fact]
    public void Equal_options_produce_equal_signatures()
    {
        var a = new EmbeddingOptions
        {
            Provider = EmbeddingProviderType.Voyage,
            Model = "voyage-3",
            Dimensions = 1024,
            ApiKey = "secret",
            Endpoint = "https://api.voyageai.com/v1",
            DatabasePath = "/tmp/x.db",
        };
        var b = new EmbeddingOptions
        {
            Provider = EmbeddingProviderType.Voyage,
            Model = "voyage-3",
            Dimensions = 1024,
            ApiKey = "secret",
            Endpoint = "https://api.voyageai.com/v1",
            DatabasePath = "/tmp/x.db",
        };

        Assert.Equal(EmbeddingOptionsSignature.From(a), EmbeddingOptionsSignature.From(b));
    }

    [Fact]
    public void Changing_api_key_changes_signature_but_does_not_leak_plaintext()
    {
        var a = new EmbeddingOptions { Provider = EmbeddingProviderType.OpenAI, ApiKey = "k1" };
        var b = new EmbeddingOptions { Provider = EmbeddingProviderType.OpenAI, ApiKey = "k2" };

        var sigA = EmbeddingOptionsSignature.From(a);
        var sigB = EmbeddingOptionsSignature.From(b);

        Assert.NotEqual(sigA, sigB);
        Assert.DoesNotContain("k1", sigA.ApiKeyHash, StringComparison.Ordinal);
        Assert.DoesNotContain("k2", sigB.ApiKeyHash, StringComparison.Ordinal);
    }

    [Fact]
    public void Empty_api_key_produces_empty_hash()
    {
        var sig = EmbeddingOptionsSignature.From(new EmbeddingOptions { Provider = EmbeddingProviderType.None });
        Assert.Equal(string.Empty, sig.ApiKeyHash);
    }
}
