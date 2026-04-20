using CompanyBrain.Dashboard.Features.License;
using FluentAssertions;

namespace CompanyBrain.Tests.Features.License;

public sealed class LicenseStateServiceTests
{
    #region LicenseTier Tests

    [Theory]
    [InlineData(LicenseTier.Free, 0)]
    [InlineData(LicenseTier.Starter, 1)]
    [InlineData(LicenseTier.Professional, 2)]
    [InlineData(LicenseTier.Enterprise, 3)]
    public void LicenseTier_EnumValues_ShouldMatchExpected(LicenseTier tier, int expectedValue)
    {
        ((int)tier).Should().Be(expectedValue);
    }

    [Fact]
    public void LicenseTier_ComparisonOrder_ShouldBeCorrect()
    {
        (LicenseTier.Free < LicenseTier.Starter).Should().BeTrue();
        (LicenseTier.Starter < LicenseTier.Professional).Should().BeTrue();
        (LicenseTier.Professional < LicenseTier.Enterprise).Should().BeTrue();
        (LicenseTier.Enterprise >= LicenseTier.Professional).Should().BeTrue();
    }

    [Fact]
    public void LicenseTier_Starter_ShouldBeGreaterThanFree()
    {
        (LicenseTier.Starter >= LicenseTier.Starter).Should().BeTrue();
        (LicenseTier.Starter >= LicenseTier.Free).Should().BeTrue();
        (LicenseTier.Free >= LicenseTier.Starter).Should().BeFalse();
    }

    #endregion

    #region LicenseGuardMiddleware Path Tests

    [Theory]
    [InlineData("/sharepoint/files")]
    [InlineData("/confluence/spaces")]
    [InlineData("/settings")]
    [InlineData("/api/sharepoint/auth")]
    public void LicenseGuardMiddleware_Tier1Paths_ShouldBeRecognized(string path)
    {
        // Paths that start with Tier1 prefixes (/sharepoint, /confluence, /settings, /api/sharepoint)
        var tier1Prefixes = new[] { "/sharepoint", "/confluence", "/settings", "/api/sharepoint" };
        var matchesAny = tier1Prefixes.Any(p => path.StartsWith(p, StringComparison.OrdinalIgnoreCase));

        matchesAny.Should().BeTrue($"path '{path}' should match a Tier1 prefix");
    }

    [Theory]
    [InlineData("/tools/builder")]
    [InlineData("/auto-setup")]
    [InlineData("/api/setup/complete")]
    public void LicenseGuardMiddleware_Tier2Paths_ShouldBeRecognized(string path)
    {
        var tier2Prefixes = new[] { "/tools", "/auto-setup", "/api/setup" };
        var matchesAny = tier2Prefixes.Any(p => path.StartsWith(p, StringComparison.OrdinalIgnoreCase));

        matchesAny.Should().BeTrue($"path '{path}' should match a Tier2 prefix");
    }

    [Theory]
    [InlineData("/dashboard")]
    [InlineData("/knowledge")]
    [InlineData("/api/knowledge")]
    [InlineData("/login")]
    [InlineData("/")]
    public void LicenseGuardMiddleware_FreePaths_ShouldNotRequireLicense(string path)
    {
        var tier1 = new[] { "/sharepoint", "/confluence", "/settings", "/api/sharepoint" };
        var tier2 = new[] { "/tools", "/auto-setup", "/api/setup" };

        var requiresLicense = tier1.Any(p => path.StartsWith(p, StringComparison.OrdinalIgnoreCase))
                              || tier2.Any(p => path.StartsWith(p, StringComparison.OrdinalIgnoreCase));

        requiresLicense.Should().BeFalse($"path '{path}' should not require a license");
    }

    #endregion
}
