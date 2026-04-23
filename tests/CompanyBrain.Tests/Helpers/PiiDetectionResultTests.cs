using CompanyBrain.Dashboard.Helpers;
using FluentAssertions;

namespace CompanyBrain.Tests.Helpers;

public sealed class PiiDetectionResultTests
{
    [Fact]
    public void TotalCount_ShouldSumAllCounts()
    {
        var result = new PiiDetectionResult
        {
            EmailCount = 1,
            ApiKeyCount = 2,
            IpAddressCount = 3,
            GitHubTokenCount = 4,
            SlackTokenCount = 5,
            AwsKeyCount = 6
        };

        result.TotalCount.Should().Be(21);
    }

    [Fact]
    public void HasPii_WhenTotalCountIsZero_ShouldReturnFalse()
    {
        var result = new PiiDetectionResult();

        result.HasPii.Should().BeFalse();
        result.TotalCount.Should().Be(0);
    }

    [Fact]
    public void HasPii_WhenAnyCountIsNonZero_ShouldReturnTrue()
    {
        var result = new PiiDetectionResult { EmailCount = 1 };

        result.HasPii.Should().BeTrue();
    }

    [Fact]
    public void DefaultValues_ShouldBeZero()
    {
        var result = new PiiDetectionResult();

        result.EmailCount.Should().Be(0);
        result.ApiKeyCount.Should().Be(0);
        result.IpAddressCount.Should().Be(0);
        result.GitHubTokenCount.Should().Be(0);
        result.SlackTokenCount.Should().Be(0);
        result.AwsKeyCount.Should().Be(0);
    }

    [Fact]
    public void TwoResultsWithSameValues_ShouldBeEqual()
    {
        var a = new PiiDetectionResult { EmailCount = 2, IpAddressCount = 1 };
        var b = new PiiDetectionResult { EmailCount = 2, IpAddressCount = 1 };

        a.Should().Be(b);
    }

    [Fact]
    public void TwoResultsWithDifferentValues_ShouldNotBeEqual()
    {
        var a = new PiiDetectionResult { EmailCount = 1 };
        var b = new PiiDetectionResult { EmailCount = 2 };

        a.Should().NotBe(b);
    }
}
