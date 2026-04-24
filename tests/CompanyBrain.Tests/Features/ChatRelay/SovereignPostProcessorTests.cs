using CompanyBrain.Dashboard.Features.ChatRelay.Services;
using FluentAssertions;
using NSubstitute;

namespace CompanyBrain.Tests.Features.ChatRelay;

public sealed class SovereignPostProcessorTests
{
    private readonly SovereignPostProcessor _sut = new();

    [Fact]
    public void Process_EmptyString_ReturnsSameString()
    {
        var result = _sut.Process(string.Empty);
        result.Should().BeEmpty();
    }

    [Fact]
    public void Process_NullSafe_ReturnsNull()
    {
        // null is handled by the null-coalesce guard
        var result = _sut.Process(null!);
        result.Should().BeNull();
    }

    [Theory]
    [InlineData("user@example.com")]
    [InlineData("admin@company.internal")]
    public void Process_Email_IsRedacted(string email)
    {
        var result = _sut.Process($"Contact {email} for details");
        result.Should().NotContain(email);
    }

    [Theory]
    [InlineData("sk-abc12345678901234567890123456")]
    [InlineData("AKIAIOSFODNN7EXAMPLE")]
    public void Process_ApiKeys_AreRedacted(string key)
    {
        var result = _sut.Process($"Use this key: {key}");
        result.Should().NotContain(key);
    }

    [Theory]
    [InlineData("192.168.1.100")]
    [InlineData("10.0.0.1")]
    public void Process_IpAddresses_AreRedacted(string ip)
    {
        var result = _sut.Process($"Server is at {ip}:5432");
        result.Should().NotContain(ip);
    }

    [Theory]
    [InlineData("WEBPROD01")]
    [InlineData("SQLNODE42")]
    [InlineData("APPDB001")]
    public void Process_WindowsServerHostname_IsRedacted(string hostname)
    {
        var result = _sut.Process($"Connect to {hostname} for the database");
        result.Should().NotContain(hostname);
        result.Should().Contain("[SERVER_REDACTED]");
    }

    [Theory]
    [InlineData("APP-SERVER-02")]
    [InlineData("DC-EAST")]
    [InlineData("WEB-FRONT-01")]
    public void Process_HyphenatedServerName_IsRedacted(string hostname)
    {
        var result = _sut.Process($"Deployed on {hostname}");
        result.Should().NotContain(hostname);
        result.Should().Contain("[SERVER_REDACTED]");
    }

    [Theory]
    [InlineData("fileserver.local")]
    [InlineData("db.corp")]
    [InlineData("intranet.internal")]
    [InlineData("proxy.lan")]
    public void Process_InternalFqdn_IsRedacted(string fqdn)
    {
        var result = _sut.Process($"Access the service at {fqdn}");
        result.Should().NotContain(fqdn);
        result.Should().Contain("[INTERNAL_HOST_REDACTED]");
    }

    [Fact]
    public void Process_NormalText_PassesThrough()
    {
        const string clean = "The quarterly report shows a 12% increase in productivity.";
        var result = _sut.Process(clean);
        result.Should().Be(clean);
    }

    [Fact]
    public void Process_MultiplePatterns_AllRedacted()
    {
        var input = "Email admin@corp.com, server WEBPROD01 at 10.0.0.1 (db.corp)";
        var result = _sut.Process(input);
        result.Should().NotContain("admin@corp.com");
        result.Should().NotContain("WEBPROD01");
        result.Should().NotContain("10.0.0.1");
        result.Should().NotContain("db.corp");
    }
}
