using CompanyBrain.Dashboard.Features.AutoSync.Models;
using CompanyBrain.Dashboard.Features.AutoSync.Providers;
using FluentAssertions;
using NSubstitute;

namespace CompanyBrain.Tests.Features.AutoSync;

public sealed class IngestionProviderFactoryTests
{
    private static IIngestionProvider MakeProvider(SourceType type)
    {
        var provider = Substitute.For<IIngestionProvider>();
        provider.SourceType.Returns(type);
        return provider;
    }

    [Fact]
    public void GetProvider_KnownSourceType_ReturnsCorrectProvider()
    {
        var webWiki = MakeProvider(SourceType.WebWiki);
        var sharePoint = MakeProvider(SourceType.SharePoint);

        var factory = new IngestionProviderFactory([webWiki, sharePoint]);

        factory.GetProvider(SourceType.WebWiki).Should().BeSameAs(webWiki);
        factory.GetProvider(SourceType.SharePoint).Should().BeSameAs(sharePoint);
    }

    [Fact]
    public void GetProvider_UnknownSourceType_ReturnsNull()
    {
        var factory = new IngestionProviderFactory([]);

        factory.GetProvider(SourceType.Notion).Should().BeNull();
    }

    [Fact]
    public void RegisteredTypes_ReflectsAllRegisteredProviders()
    {
        var providers = new[]
        {
            MakeProvider(SourceType.WebWiki),
            MakeProvider(SourceType.GitHub),
            MakeProvider(SourceType.Confluence),
        };

        var factory = new IngestionProviderFactory(providers);

        factory.RegisteredTypes.Should().BeEquivalentTo(
            [SourceType.WebWiki, SourceType.GitHub, SourceType.Confluence]);
    }

    [Fact]
    public void Constructor_WithEmptyProviders_DoesNotThrow()
    {
        var act = () => new IngestionProviderFactory([]);
        act.Should().NotThrow();
    }
}
