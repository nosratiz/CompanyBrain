using CompanyBrain.Dashboard.Features.ChatRelay.Services;
using FluentAssertions;

namespace CompanyBrain.Tests.Features.ChatRelay;

/// <summary>
/// Integration-level smoke tests for ChatRelayService orchestration.
///
/// <para>
/// The core dependencies (ChatRelaySettingsService, KnowledgeApplicationService) are sealed
/// concrete types that require full infrastructure (EF Core, Data Protection) to instantiate.
/// Detailed orchestration tests belong in integration tests that spin up a test host.
/// These tests verify only the post-processor integration path directly.
/// </para>
/// </summary>
public sealed class ChatRelayServiceTests
{
    private readonly SovereignPostProcessor _postProcessor = new();

    [Fact]
    public void PostProcessor_RedactsAnswerBeforeReply()
    {
        var sensitive = "The server is WEBPROD01 at 192.168.1.1 (db.corp).";
        var result = _postProcessor.Process(sensitive);

        result.Should().NotContain("WEBPROD01");
        result.Should().NotContain("192.168.1.1");
        result.Should().NotContain("db.corp");
    }

    [Fact]
    public void PostProcessor_CleanAnswer_PassesThrough()
    {
        const string clean = "You can find the onboarding guide in the HR section of the knowledge base.";
        _postProcessor.Process(clean).Should().Be(clean);
    }
}

