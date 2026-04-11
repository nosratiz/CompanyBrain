using CompanyBrain.Utilities;
using FluentAssertions;

namespace CompanyBrain.Tests.Utilities;

public sealed class MarkdownUtilitiesTests
{
    #region Normalize Tests

    [Fact]
    public void Normalize_WithWindowsLineEndings_ShouldConvertToUnix()
    {
        // Arrange
        var input = "Line 1\r\nLine 2\r\nLine 3";

        // Act
        var result = MarkdownUtilities.Normalize(input);

        // Assert
        result.Should().Be("Line 1\nLine 2\nLine 3");
        result.Should().NotContain("\r");
    }

    [Fact]
    public void Normalize_WithExcessBlankLines_ShouldReduceToTwoNewlines()
    {
        // Arrange
        var input = "Paragraph 1\n\n\n\n\nParagraph 2";

        // Act
        var result = MarkdownUtilities.Normalize(input);

        // Assert
        result.Should().Be("Paragraph 1\n\nParagraph 2");
    }

    [Fact]
    public void Normalize_WithLeadingAndTrailingWhitespace_ShouldTrim()
    {
        // Arrange
        var input = "   \n\nContent here\n\n   ";

        // Act
        var result = MarkdownUtilities.Normalize(input);

        // Assert
        result.Should().Be("Content here");
    }

    [Theory]
    [InlineData("Single line", "Single line")]
    [InlineData("Line 1\nLine 2", "Line 1\nLine 2")]
    [InlineData("Para 1\n\nPara 2", "Para 1\n\nPara 2")]
    public void Normalize_WithValidMarkdown_ShouldPreserve(string input, string expected)
    {
        // Act
        var result = MarkdownUtilities.Normalize(input);

        // Assert
        result.Should().Be(expected);
    }

    [Fact]
    public void Normalize_WithMixedLineEndings_ShouldNormalizeCRLF()
    {
        // Arrange - \r\n should be normalized to \n
        var input = "Line 1\r\nLine 2\r\nLine 3";

        // Act
        var result = MarkdownUtilities.Normalize(input);

        // Assert
        result.Should().NotContain("\r\n");
        result.Should().Contain("\n");
    }

    [Fact]
    public void Normalize_WithEmpty_ShouldReturnEmpty()
    {
        // Act
        var result = MarkdownUtilities.Normalize("");

        // Assert
        result.Should().BeEmpty();
    }

    #endregion

    #region EscapeTableCell Tests

    [Fact]
    public void EscapeTableCell_WithPipeCharacter_ShouldEscape()
    {
        // Act
        var result = MarkdownUtilities.EscapeTableCell("Value | with | pipes");

        // Assert
        result.Should().Be(@"Value \| with \| pipes");
    }

    [Fact]
    public void EscapeTableCell_WithNewlines_ShouldReplaceWithSpace()
    {
        // Act
        var result = MarkdownUtilities.EscapeTableCell("Multi\nline\nvalue");

        // Assert
        result.Should().Be("Multi line value");
    }

    [Fact]
    public void EscapeTableCell_WithLeadingTrailingWhitespace_ShouldTrim()
    {
        // Act
        var result = MarkdownUtilities.EscapeTableCell("   value   ");

        // Assert
        result.Should().Be("value");
    }

    [Fact]
    public void EscapeTableCell_WithPipesAndNewlines_ShouldEscapeAndReplace()
    {
        // Act
        var result = MarkdownUtilities.EscapeTableCell("Value | A\nValue | B");

        // Assert
        result.Should().Be(@"Value \| A Value \| B");
    }

    [Fact]
    public void EscapeTableCell_WithEmptyString_ShouldReturnEmpty()
    {
        // Act
        var result = MarkdownUtilities.EscapeTableCell("");

        // Assert
        result.Should().BeEmpty();
    }

    [Theory]
    [InlineData("simple text", "simple text")]
    [InlineData("numbers 123", "numbers 123")]
    [InlineData("special!@#$%", "special!@#$%")]
    public void EscapeTableCell_WithoutSpecialChars_ShouldReturnUnchanged(string input, string expected)
    {
        // Act
        var result = MarkdownUtilities.EscapeTableCell(input);

        // Assert
        result.Should().Be(expected);
    }

    #endregion

    #region ToBlockQuote Tests

    [Fact]
    public void ToBlockQuote_WithSingleLine_ShouldAddPrefix()
    {
        // Act
        var result = MarkdownUtilities.ToBlockQuote("This is a quote");

        // Assert
        result.Should().Be("> This is a quote");
    }

    [Fact]
    public void ToBlockQuote_WithMultipleLines_ShouldAddPrefixToEach()
    {
        // Arrange
        var input = "Line 1\nLine 2\nLine 3";

        // Act
        var result = MarkdownUtilities.ToBlockQuote(input);

        // Assert
        result.Should().Contain("> Line 1");
        result.Should().Contain("> Line 2");
        result.Should().Contain("> Line 3");
    }

    [Fact]
    public void ToBlockQuote_WithEmptyLines_ShouldSkipEmpty()
    {
        // Arrange
        var input = "Line 1\n\n\nLine 2";

        // Act
        var result = MarkdownUtilities.ToBlockQuote(input);

        // Assert
        // Empty lines should be skipped due to StringSplitOptions.RemoveEmptyEntries
        result.Should().NotContain(">\n>");
    }

    [Fact]
    public void ToBlockQuote_WithLeadingWhitespace_ShouldTrimLines()
    {
        // Arrange
        var input = "   Line with spaces   ";

        // Act
        var result = MarkdownUtilities.ToBlockQuote(input);

        // Assert
        result.Should().Be("> Line with spaces");
    }

    [Fact]
    public void ToBlockQuote_WithWindowsLineEndings_ShouldNormalizeFirst()
    {
        // Arrange
        var input = "Line 1\r\nLine 2";

        // Act
        var result = MarkdownUtilities.ToBlockQuote(input);

        // Assert
        result.Should().Contain("> Line 1");
        result.Should().Contain("> Line 2");
    }

    #endregion
}
