using CompanyBrain.Dashboard.Helpers;
using FluentAssertions;

namespace CompanyBrain.Tests.Helpers;

public sealed class SecurityHelpersTests
{
    #region IsPathSafe Tests

    [Theory]
    [InlineData("file.txt", "/base/tenant", true)]
    [InlineData("subdir/file.txt", "/base/tenant", true)]
    [InlineData("a/b/c/file.txt", "/base/tenant", true)]
    public void IsPathSafe_WithValidRelativePath_ShouldReturnTrue(string path, string basePath, bool expected)
    {
        SecurityHelpers.IsPathSafe(path, basePath).Should().Be(expected);
    }

    [Theory]
    [InlineData("../escape.txt", "/base/tenant")]
    [InlineData("../../etc/passwd", "/base/tenant")]
    [InlineData("subdir/../../../escape.txt", "/base/tenant")]
    [InlineData("valid/../../escape.txt", "/base/tenant")]
    public void IsPathSafe_WithDirectoryTraversal_ShouldReturnFalse(string path, string basePath)
    {
        SecurityHelpers.IsPathSafe(path, basePath).Should().BeFalse();
    }

    [Theory]
    [InlineData("", "/base")]
    [InlineData("   ", "/base")]
    [InlineData(null, "/base")]
    public void IsPathSafe_WithEmptyPath_ShouldReturnFalse(string? path, string basePath)
    {
        SecurityHelpers.IsPathSafe(path!, basePath).Should().BeFalse();
    }

    [Theory]
    [InlineData("file.txt", "")]
    [InlineData("file.txt", "   ")]
    [InlineData("file.txt", null)]
    public void IsPathSafe_WithEmptyBasePath_ShouldReturnFalse(string path, string? basePath)
    {
        SecurityHelpers.IsPathSafe(path, basePath!).Should().BeFalse();
    }

    [Fact]
    public void IsPathSafe_WithNestedButSafePath_ShouldReturnTrue()
    {
        // Path with .. but still resolves within base
        var result = SecurityHelpers.IsPathSafe("subdir/../other/file.txt", "/base/tenant");
        result.Should().BeTrue();
    }

    #endregion

    #region IsAbsolutePathSafe Tests

    [Fact]
    public void IsAbsolutePathSafe_WithPathWithinBase_ShouldReturnTrue()
    {
        var result = SecurityHelpers.IsAbsolutePathSafe("/base/tenant/docs/file.txt", "/base/tenant");
        result.Should().BeTrue();
    }

    [Fact]
    public void IsAbsolutePathSafe_WithPathOutsideBase_ShouldReturnFalse()
    {
        var result = SecurityHelpers.IsAbsolutePathSafe("/other/path/file.txt", "/base/tenant");
        result.Should().BeFalse();
    }

    [Theory]
    [InlineData("", "/base")]
    [InlineData("/path", "")]
    [InlineData(null, "/base")]
    [InlineData("/path", null)]
    public void IsAbsolutePathSafe_WithEmptyInputs_ShouldReturnFalse(string? absolutePath, string? basePath)
    {
        SecurityHelpers.IsAbsolutePathSafe(absolutePath!, basePath!).Should().BeFalse();
    }

    #endregion

    #region RedactPii Tests

    [Theory]
    [InlineData("user@example.com", "[EMAIL_REDACTED]")]
    [InlineData("test.user+tag@domain.co.uk", "[EMAIL_REDACTED]")]
    public void RedactPii_WithEmail_ShouldRedact(string input, string expected)
    {
        SecurityHelpers.RedactPii(input).Should().Be(expected);
    }

    [Theory]
    [InlineData("sk-1234567890abcdefghij", "[API_KEY_REDACTED]")]
    [InlineData("sk-abcdefghijklmnopqrstuvwxyz", "[API_KEY_REDACTED]")]
    public void RedactPii_WithApiKey_ShouldRedact(string input, string expected)
    {
        SecurityHelpers.RedactPii(input).Should().Be(expected);
    }

    [Theory]
    [InlineData("192.168.1.1", "[IP_REDACTED]")]
    [InlineData("10.0.0.1", "[IP_REDACTED]")]
    [InlineData("255.255.255.255", "[IP_REDACTED]")]
    public void RedactPii_WithIpAddress_ShouldRedact(string input, string expected)
    {
        SecurityHelpers.RedactPii(input).Should().Be(expected);
    }

    [Theory]
    [InlineData("ghp_1234567890abcdefghijklmnopqrstuvwxyz")]
    [InlineData("gho_1234567890abcdefghijklmnopqrstuvwxyz")]
    [InlineData("ghu_1234567890abcdefghijklmnopqrstuvwxyz")]
    [InlineData("ghs_1234567890abcdefghijklmnopqrstuvwxyz")]
    [InlineData("ghr_1234567890abcdefghijklmnopqrstuvwxyz")]
    public void RedactPii_WithGitHubToken_ShouldRedact(string input)
    {
        var result = SecurityHelpers.RedactPii(input);
        result.Should().Be("[GITHUB_TOKEN_REDACTED]");
    }

    [Theory]
    [InlineData("xoxb-token-here")]
    [InlineData("xoxp-workspace-token")]
    [InlineData("xoxa-app-token")]
    public void RedactPii_WithSlackToken_ShouldRedact(string input)
    {
        var result = SecurityHelpers.RedactPii(input);
        result.Should().Be("[SLACK_TOKEN_REDACTED]");
    }

    [Fact]
    public void RedactPii_WithAwsAccessKey_ShouldRedact()
    {
        var input = "AKIAIOSFODNN7EXAMPLE";
        var result = SecurityHelpers.RedactPii(input);
        result.Should().Be("[AWS_KEY_REDACTED]");
    }

    [Fact]
    public void RedactPii_WithMultiplePii_ShouldRedactAll()
    {
        var input = "Contact user@example.com at 192.168.1.1 using key sk-abcdefghijklmnopqrstuvwxyz";
        var result = SecurityHelpers.RedactPii(input);
        
        result.Should().Contain("[EMAIL_REDACTED]");
        result.Should().Contain("[IP_REDACTED]");
        result.Should().Contain("[API_KEY_REDACTED]");
        result.Should().NotContain("user@example.com");
        result.Should().NotContain("192.168.1.1");
        result.Should().NotContain("sk-");
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public void RedactPii_WithEmptyInput_ShouldReturnAsIs(string? input)
    {
        SecurityHelpers.RedactPii(input!).Should().Be(input);
    }

    [Fact]
    public void RedactPii_WithNoSensitiveData_ShouldReturnUnchanged()
    {
        var input = "This is a normal text without any PII";
        SecurityHelpers.RedactPii(input).Should().Be(input);
    }

    [Theory]
    [InlineData("0684304623")]                   // Dutch mobile (as it appears in the CV)
    [InlineData("0612345678")]                   // Generic Dutch mobile
    [InlineData("0201234567")]                   // Dutch landline (Amsterdam)
    public void RedactPii_WithDutchPhoneNumber_ShouldRedact(string phone)
    {
        var result = SecurityHelpers.RedactPii($"Call me at {phone}");
        result.Should().NotContain(phone);
        result.Should().Contain("[PHONE_REDACTED]");
    }

    [Theory]
    [InlineData("+31 6 84304623")]
    [InlineData("+1 800 555 1234")]
    public void RedactPii_WithInternationalPhoneNumber_ShouldRedact(string phone)
    {
        var result = SecurityHelpers.RedactPii($"Reach me at {phone} anytime");
        result.Should().NotContain(phone);
        result.Should().Contain("[PHONE_REDACTED]");
    }

    #endregion

    #region ContainsPii Tests

    [Theory]
    [InlineData("user@example.com", true)]
    [InlineData("sk-12345678901234567890", true)]
    [InlineData("192.168.1.1", true)]
    [InlineData("ghp_1234567890abcdefghijklmnopqrstuvwxyz", true)]
    [InlineData("xoxb-token-here", true)]
    [InlineData("AKIAIOSFODNN7EXAMPLE", true)]
    [InlineData("Normal text", false)]
    [InlineData("", false)]
    public void ContainsPii_ShouldDetectCorrectly(string input, bool expected)
    {
        SecurityHelpers.ContainsPii(input).Should().Be(expected);
    }

    #endregion

    #region DetectPii Tests

    [Fact]
    public void DetectPii_WithMixedContent_ShouldCountCorrectly()
    {
        var input = "Contact user1@test.com and user2@example.org at 10.0.0.1 or 192.168.1.1";
        
        var result = SecurityHelpers.DetectPii(input);
        
        result.EmailCount.Should().Be(2);
        result.IpAddressCount.Should().Be(2);
        result.ApiKeyCount.Should().Be(0);
        result.TotalCount.Should().Be(4);
        result.HasPii.Should().BeTrue();
    }

    [Fact]
    public void DetectPii_WithNoPii_ShouldReturnZeros()
    {
        var result = SecurityHelpers.DetectPii("Just normal text here");
        
        result.TotalCount.Should().Be(0);
        result.HasPii.Should().BeFalse();
    }

    [Fact]
    public void DetectPii_WithEmptyString_ShouldReturnZeros()
    {
        var result = SecurityHelpers.DetectPii("");
        
        result.TotalCount.Should().Be(0);
        result.HasPii.Should().BeFalse();
    }

    #endregion

    #region MatchesExcludedPattern Tests

    [Theory]
    [InlineData("secrets.env", new[] { "*.env" }, true)]
    [InlineData(".env", new[] { ".env" }, true)]
    [InlineData("config/secrets.json", new[] { "*.secret", "*.json" }, true)]
    [InlineData("src/credentials/key.txt", new[] { "**/credentials/**" }, true)]
    public void MatchesExcludedPattern_WithMatchingPatterns_ShouldReturnTrue(
        string path, string[] patterns, bool expected)
    {
        SecurityHelpers.MatchesExcludedPattern(path, patterns).Should().Be(expected);
    }

    [Theory]
    [InlineData("normal.txt", new[] { "*.env", "*.secret" })]
    [InlineData("src/app.cs", new[] { "**/credentials/**" })]
    public void MatchesExcludedPattern_WithNonMatchingPatterns_ShouldReturnFalse(
        string path, string[] patterns)
    {
        SecurityHelpers.MatchesExcludedPattern(path, patterns).Should().BeFalse();
    }

    [Fact]
    public void MatchesExcludedPattern_WithEmptyPatterns_ShouldReturnFalse()
    {
        SecurityHelpers.MatchesExcludedPattern("file.txt", []).Should().BeFalse();
    }

    [Fact]
    public void MatchesExcludedPattern_WithEmptyPath_ShouldReturnFalse()
    {
        SecurityHelpers.MatchesExcludedPattern("", ["*.txt"]).Should().BeFalse();
    }

    [Fact]
    public void MatchesExcludedPattern_WithWhitespacePatterns_ShouldSkipThem()
    {
        var patterns = new[] { "  ", "", "*.secret" };
        SecurityHelpers.MatchesExcludedPattern("config.secret", patterns).Should().BeTrue();
        SecurityHelpers.MatchesExcludedPattern("normal.txt", patterns).Should().BeFalse();
    }

    #endregion
}
