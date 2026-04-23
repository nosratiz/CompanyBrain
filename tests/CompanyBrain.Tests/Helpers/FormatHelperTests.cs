using System.Globalization;
using CompanyBrain.Dashboard.Helpers;
using FluentAssertions;

namespace CompanyBrain.Tests.Helpers;

public sealed class FormatHelperTests
{
    #region FormatBytes Tests

    [Theory]
    [InlineData(0L, "0 B")]
    [InlineData(500L, "500 B")]
    [InlineData(1023L, "1023 B")]
    [InlineData(1024L, "1 KB")]
    [InlineData(2048L, "2 KB")]
    [InlineData(1048576L, "1 MB")]
    [InlineData(1073741824L, "1 GB")]
    [InlineData(1099511627776L, "1 TB")]
    public void FormatBytes_ShouldFormatCorrectly(long bytes, string expected)
    {
        FormatHelper.FormatBytes(bytes).Should().Be(expected);
    }

    [Fact]
    public void FormatBytes_WithDecimalKb_ShouldContainValue()
    {
        // 1536 bytes = 1.5 KB — decimal separator varies by locale
        var result = FormatHelper.FormatBytes(1536L);
        result.Should().EndWith("KB");
        result.Should().MatchRegex(@"1[.,]5 KB");
    }

    [Fact]
    public void FormatBytes_WithDecimalMb_ShouldContainValue()
    {
        // 1572864 bytes = 1.5 MB — decimal separator varies by locale
        var result = FormatHelper.FormatBytes(1572864L);
        result.Should().EndWith("MB");
        result.Should().MatchRegex(@"1[.,]5 MB");
    }

    [Fact]
    public void FormatBytes_WhenNegative_ShouldReturnZeroB()
    {
        FormatHelper.FormatBytes(-1).Should().Be("0 B");
        FormatHelper.FormatBytes(-1000).Should().Be("0 B");
    }

    [Fact]
    public void FormatBytes_WhenZero_ShouldReturnZeroB()
    {
        FormatHelper.FormatBytes(0).Should().Be("0 B");
    }

    [Fact]
    public void FormatBytes_WhenExactlyOneByte_ShouldReturnOneB()
    {
        FormatHelper.FormatBytes(1).Should().Be("1 B");
    }

    #endregion

    #region Truncate Tests

    [Fact]
    public void Truncate_WhenValueFitsWithinMaxLength_ShouldReturnUnchanged()
    {
        FormatHelper.Truncate("Hello", 10).Should().Be("Hello");
    }

    [Fact]
    public void Truncate_WhenValueEqualsMaxLength_ShouldReturnUnchanged()
    {
        FormatHelper.Truncate("Hello", 5).Should().Be("Hello");
    }

    [Fact]
    public void Truncate_WhenValueExceedsMaxLength_ShouldTruncateWithEllipsis()
    {
        FormatHelper.Truncate("Hello World", 8).Should().Be("Hello...");
    }

    [Fact]
    public void Truncate_WhenMaxLengthIsThreeOrLess_ShouldTruncateWithoutEllipsis()
    {
        FormatHelper.Truncate("Hello", 3).Should().Be("Hel");
        FormatHelper.Truncate("Hello", 2).Should().Be("He");
        FormatHelper.Truncate("Hello", 1).Should().Be("H");
    }

    [Fact]
    public void Truncate_WhenValueIsEmpty_ShouldReturnEmpty()
    {
        FormatHelper.Truncate("", 10).Should().Be(string.Empty);
    }

    [Fact]
    public void Truncate_WhenValueIsNull_ShouldReturnEmpty()
    {
        FormatHelper.Truncate(null!, 10).Should().Be(string.Empty);
    }

    [Fact]
    public void Truncate_WithShortValue_ShouldAppendEllipsis()
    {
        FormatHelper.Truncate("Hello World", 5).Should().Be("He...");
    }

    #endregion

    #region StripResourcesPrefix Tests

    [Fact]
    public void StripResourcesPrefix_WhenPrefixPresent_ShouldRemoveIt()
    {
        FormatHelper.StripResourcesPrefix("resources/my-document").Should().Be("my-document");
    }

    [Fact]
    public void StripResourcesPrefix_WhenPrefixAbsent_ShouldReturnUnchanged()
    {
        FormatHelper.StripResourcesPrefix("my-document").Should().Be("my-document");
    }

    [Fact]
    public void StripResourcesPrefix_IsCaseInsensitive()
    {
        FormatHelper.StripResourcesPrefix("RESOURCES/My-Document").Should().Be("My-Document");
        FormatHelper.StripResourcesPrefix("Resources/Doc").Should().Be("Doc");
    }

    [Fact]
    public void StripResourcesPrefix_WhenOnlyPrefix_ShouldReturnEmpty()
    {
        FormatHelper.StripResourcesPrefix("resources/").Should().Be("");
    }

    [Fact]
    public void StripResourcesPrefix_WithNestedPath_ShouldStripOnlyPrefix()
    {
        FormatHelper.StripResourcesPrefix("resources/folder/document").Should().Be("folder/document");
    }

    #endregion
}
