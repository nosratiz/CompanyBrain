using CompanyBrain.Utilities;
using FluentAssertions;
using HtmlAgilityPack;

namespace CompanyBrain.Tests.Utilities;

public sealed class HtmlMarkdownConverterTests
{
    private readonly Uri _baseUri = new("https://example.com/docs/");

    private HtmlNode ParseHtml(string html)
    {
        var doc = new HtmlDocument();
        doc.LoadHtml(html);
        return doc.DocumentNode;
    }

    #region Basic Text Conversion Tests

    [Fact]
    public void Convert_WithPlainText_ShouldReturnText()
    {
        // Arrange
        var html = "<div>Hello World</div>";

        // Act
        var result = HtmlMarkdownConverter.Convert(ParseHtml(html), _baseUri);

        // Assert
        result.Should().Contain("Hello World");
    }

    [Fact]
    public void Convert_WithComments_ShouldIgnoreComments()
    {
        // Arrange
        var html = "<div>Visible<!-- hidden comment -->Content</div>";

        // Act
        var result = HtmlMarkdownConverter.Convert(ParseHtml(html), _baseUri);

        // Assert
        result.Should().NotContain("hidden comment");
        result.Should().Contain("Visible");
        result.Should().Contain("Content");
    }

    #endregion

    #region Heading Tests

    [Theory]
    [InlineData("<h1>Title</h1>", "# Title")]
    [InlineData("<h2>Subtitle</h2>", "## Subtitle")]
    [InlineData("<h3>Section</h3>", "### Section")]
    [InlineData("<h4>Subsection</h4>", "#### Subsection")]
    [InlineData("<h5>Minor</h5>", "##### Minor")]
    [InlineData("<h6>Tiny</h6>", "###### Tiny")]
    public void Convert_WithHeadings_ShouldConvertToMarkdownHeadings(string html, string expected)
    {
        // Act
        var result = HtmlMarkdownConverter.Convert(ParseHtml(html), _baseUri);

        // Assert
        result.Should().Contain(expected);
    }

    [Fact]
    public void Convert_WithNestedHeading_ShouldExtractText()
    {
        // Arrange
        var html = "<h1><span>Nested <strong>Bold</strong> Title</span></h1>";

        // Act
        var result = HtmlMarkdownConverter.Convert(ParseHtml(html), _baseUri);

        // Assert
        result.Should().Contain("# Nested Bold Title");
    }

    #endregion

    #region Paragraph and Line Break Tests

    [Fact]
    public void Convert_WithParagraph_ShouldAddDoubleNewline()
    {
        // Arrange
        var html = "<p>First paragraph</p><p>Second paragraph</p>";

        // Act
        var result = HtmlMarkdownConverter.Convert(ParseHtml(html), _baseUri);

        // Assert
        result.Should().Contain("First paragraph");
        result.Should().Contain("Second paragraph");
    }

    [Fact]
    public void Convert_WithBreak_ShouldAddNewline()
    {
        // Arrange
        var html = "<div>Line 1<br/>Line 2</div>";

        // Act
        var result = HtmlMarkdownConverter.Convert(ParseHtml(html), _baseUri);

        // Assert
        result.Should().Contain("Line 1");
        result.Should().Contain("Line 2");
    }

    #endregion

    #region List Tests

    [Fact]
    public void Convert_WithUnorderedList_ShouldConvertToDashItems()
    {
        // Arrange
        var html = "<ul><li>Item 1</li><li>Item 2</li><li>Item 3</li></ul>";

        // Act
        var result = HtmlMarkdownConverter.Convert(ParseHtml(html), _baseUri);

        // Assert
        result.Should().Contain("- Item 1");
        result.Should().Contain("- Item 2");
        result.Should().Contain("- Item 3");
    }

    [Fact]
    public void Convert_WithOrderedList_ShouldConvertToDashItems()
    {
        // Arrange - ordered lists also use dash format
        var html = "<ol><li>First</li><li>Second</li></ol>";

        // Act
        var result = HtmlMarkdownConverter.Convert(ParseHtml(html), _baseUri);

        // Assert
        result.Should().Contain("- First");
        result.Should().Contain("- Second");
    }

    [Fact]
    public void Convert_WithNestedList_ShouldIndent()
    {
        // Arrange
        var html = @"
            <ul>
                <li>Parent
                    <ul>
                        <li>Child</li>
                    </ul>
                </li>
            </ul>";

        // Act
        var result = HtmlMarkdownConverter.Convert(ParseHtml(html), _baseUri);

        // Assert
        result.Should().Contain("- Parent");
        result.Should().Contain("- Child");
    }

    #endregion

    #region Link Tests

    [Fact]
    public void Convert_WithAbsoluteLink_ShouldConvertToMarkdownLink()
    {
        // Arrange
        var html = "<a href=\"https://other.com/page\">Link Text</a>";

        // Act
        var result = HtmlMarkdownConverter.Convert(ParseHtml(html), _baseUri);

        // Assert
        result.Should().Contain("[Link Text](https://other.com/page)");
    }

    [Fact]
    public void Convert_WithRelativeLink_ShouldResolveAgainstBaseUri()
    {
        // Arrange
        var html = "<a href=\"../other-page.html\">Relative Link</a>";

        // Act
        var result = HtmlMarkdownConverter.Convert(ParseHtml(html), _baseUri);

        // Assert
        result.Should().Contain("[Relative Link](https://example.com/other-page.html)");
    }

    [Fact]
    public void Convert_WithEmptyLinkText_ShouldSkip()
    {
        // Arrange
        var html = "<a href=\"https://example.com\">   </a>";

        // Act
        var result = HtmlMarkdownConverter.Convert(ParseHtml(html), _baseUri);

        // Assert
        result.Should().NotContain("[](");
    }

    [Fact]
    public void Convert_WithLinkWithNoHref_ShouldOutputTextOnly()
    {
        // Arrange
        var html = "<a>Just text</a>";

        // Act
        var result = HtmlMarkdownConverter.Convert(ParseHtml(html), _baseUri);

        // Assert
        result.Should().Contain("Just text");
        result.Should().NotContain("[");
    }

    #endregion

    #region Code Tests

    [Fact]
    public void Convert_WithPreformattedCode_ShouldWrapInCodeBlock()
    {
        // Arrange
        var html = "<pre>function test() {\n  return true;\n}</pre>";

        // Act
        var result = HtmlMarkdownConverter.Convert(ParseHtml(html), _baseUri);

        // Assert
        result.Should().Contain("```text");
        result.Should().Contain("function test()");
        result.Should().Contain("```");
    }

    [Fact]
    public void Convert_WithInlineCode_ShouldWrapInBackticks()
    {
        // Arrange
        var html = "<p>Use the <code>console.log()</code> function.</p>";

        // Act
        var result = HtmlMarkdownConverter.Convert(ParseHtml(html), _baseUri);

        // Assert
        result.Should().Contain("`console.log()`");
    }

    #endregion

    #region Table Tests

    [Fact]
    public void Convert_WithSimpleTable_ShouldConvertToMarkdownTable()
    {
        // Arrange
        var html = @"
            <table>
                <tr><th>Header 1</th><th>Header 2</th></tr>
                <tr><td>Cell 1</td><td>Cell 2</td></tr>
            </table>";

        // Act
        var result = HtmlMarkdownConverter.Convert(ParseHtml(html), _baseUri);

        // Assert
        result.Should().Contain("| Header 1 | Header 2 |");
        result.Should().Contain("| --- | --- |");
        result.Should().Contain("| Cell 1 | Cell 2 |");
    }

    [Fact]
    public void Convert_WithTableContainingPipes_ShouldEscapePipes()
    {
        // Arrange
        var html = @"
            <table>
                <tr><th>Command</th></tr>
                <tr><td>cmd | grep</td></tr>
            </table>";

        // Act
        var result = HtmlMarkdownConverter.Convert(ParseHtml(html), _baseUri);

        // Assert
        result.Should().Contain(@"cmd \| grep");
    }

    #endregion

    #region Section and Div Tests

    [Fact]
    public void Convert_WithDivAndSection_ShouldAddNewlines()
    {
        // Arrange
        var html = "<div>Content 1</div><section>Content 2</section><article>Content 3</article>";

        // Act
        var result = HtmlMarkdownConverter.Convert(ParseHtml(html), _baseUri);

        // Assert
        result.Should().Contain("Content 1");
        result.Should().Contain("Content 2");
        result.Should().Contain("Content 3");
    }

    #endregion

    #region Whitespace Normalization Tests

    [Fact]
    public void Convert_WithExcessiveWhitespace_ShouldNormalize()
    {
        // Arrange
        var html = "<p>Text    with    spaces</p>";

        // Act
        var result = HtmlMarkdownConverter.Convert(ParseHtml(html), _baseUri);

        // Assert
        result.Should().Contain("Text with spaces");
    }

    [Fact]
    public void Convert_WithHtmlEntities_ShouldDecode()
    {
        // Arrange
        var html = "<pre>&lt;div&gt;escaped&lt;/div&gt;</pre>";

        // Act
        var result = HtmlMarkdownConverter.Convert(ParseHtml(html), _baseUri);

        // Assert
        result.Should().Contain("<div>escaped</div>");
    }

    #endregion
}
