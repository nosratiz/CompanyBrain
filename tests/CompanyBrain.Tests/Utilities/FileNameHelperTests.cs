using CompanyBrain.Utilities;
using FluentAssertions;

namespace CompanyBrain.Tests.Utilities;

public sealed class FileNameHelperTests
{
    #region ToMarkdownFileName Basic Tests

    [Fact]
    public void ToMarkdownFileName_WithSimpleName_ShouldReturnSlugWithMdExtension()
    {
        // Act
        var result = FileNameHelper.ToMarkdownFileName("My Document");

        // Assert
        result.Should().Be("my-document.md");
    }

    [Fact]
    public void ToMarkdownFileName_WithExistingMdExtension_ShouldNotDuplicateExtension()
    {
        // Act
        var result = FileNameHelper.ToMarkdownFileName("readme.md");

        // Assert
        result.Should().Be("readme.md");
    }

    [Fact]
    public void ToMarkdownFileName_WithUppercaseMdExtension_ShouldNormalize()
    {
        // Act
        var result = FileNameHelper.ToMarkdownFileName("README.MD");

        // Assert
        result.Should().Be("readme.md");
    }

    #endregion

    #region Special Characters Tests

    [Theory]
    [InlineData("Hello World!", "hello-world.md")]
    [InlineData("Test@#$%File", "test-file.md")]
    [InlineData("My   File   Name", "my-file-name.md")]
    [InlineData("Under_Score_Name", "under_score_name.md")]
    [InlineData("Mixed.Dots.Here", "mixed.dots.here.md")]
    public void ToMarkdownFileName_WithSpecialCharacters_ShouldSanitize(string input, string expected)
    {
        // Act
        var result = FileNameHelper.ToMarkdownFileName(input);

        // Assert
        result.Should().Be(expected);
    }

    [Theory]
    [InlineData("---name---", "name.md")]
    [InlineData("--start", "start.md")]
    [InlineData("end--", "end.md")]
    public void ToMarkdownFileName_WithLeadingTrailingHyphens_ShouldTrim(string input, string expected)
    {
        // Act
        var result = FileNameHelper.ToMarkdownFileName(input);

        // Assert
        result.Should().Be(expected);
    }

    [Theory]
    [InlineData("test----multiple----hyphens", "test-multiple-hyphens.md")]
    [InlineData("a------b", "a-b.md")]
    public void ToMarkdownFileName_WithMultipleConsecutiveHyphens_ShouldReduceToSingle(string input, string expected)
    {
        // Act
        var result = FileNameHelper.ToMarkdownFileName(input);

        // Assert
        result.Should().Be(expected);
    }

    #endregion

    #region Edge Cases Tests

    [Fact]
    public void ToMarkdownFileName_WithNull_ShouldReturnDefaultDocument()
    {
        // Act
        var result = FileNameHelper.ToMarkdownFileName(null!);

        // Assert
        result.Should().Be("document.md");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\t\n")]
    public void ToMarkdownFileName_WithEmptyOrWhitespace_ShouldReturnDocumentMd(string input)
    {
        // Act
        var result = FileNameHelper.ToMarkdownFileName(input);

        // Assert - Empty/whitespace defaults to "document" which survives slugification
        result.Should().Be("document.md");
    }

    [Theory]
    [InlineData("@#$%^&*()")]
    [InlineData("!!!")]
    public void ToMarkdownFileName_WithOnlyInvalidCharacters_ShouldReturnTimestampedDefault(string input)
    {
        // Act
        var result = FileNameHelper.ToMarkdownFileName(input);

        // Assert
        result.Should().StartWith("document-");
        result.Should().MatchRegex(@"document-\d{14}\.md");
    }

    #endregion

    #region Unicode and International Tests

    [Theory]
    [InlineData("日本語ドキュメント", "document-")] // Japanese - will result in default
    [InlineData("Документ", "document-")] // Russian - will result in default
    [InlineData("文档名称", "document-")] // Chinese - will result in default
    public void ToMarkdownFileName_WithNonAsciiCharacters_ShouldReturnTimestampedDefault(string input, string expectedPrefix)
    {
        // Act
        var result = FileNameHelper.ToMarkdownFileName(input);

        // Assert
        result.Should().StartWith(expectedPrefix);
        result.Should().EndWith(".md");
    }

    [Theory]
    [InlineData("Café Menu", "caf-menu.md")]
    [InlineData("naïve résumé", "na-ve-r-sum.md")]
    public void ToMarkdownFileName_WithAccentedCharacters_ShouldRemoveAccents(string input, string expected)
    {
        // Act
        var result = FileNameHelper.ToMarkdownFileName(input);

        // Assert
        result.Should().Be(expected);
    }

    #endregion

    #region Numbers and Alphanumeric Tests

    [Theory]
    [InlineData("file123", "file123.md")]
    [InlineData("123file", "123file.md")]
    [InlineData("file-123-name", "file-123-name.md")]
    [InlineData("version2.0", "version2.0.md")]
    public void ToMarkdownFileName_WithNumbers_ShouldPreserve(string input, string expected)
    {
        // Act
        var result = FileNameHelper.ToMarkdownFileName(input);

        // Assert
        result.Should().Be(expected);
    }

    #endregion
}
