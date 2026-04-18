using CompanyBrain.Utilities;
using FluentAssertions;

namespace CompanyBrain.Tests.Utilities;

public sealed class SearchUtilitiesTests
{
    #region Tokenize Tests

    [Fact]
    public void Tokenize_WithSimpleQuery_ShouldReturnLowercaseTokens()
    {
        // Act
        var result = SearchUtilities.Tokenize("Hello World").ToList();

        // Assert
        result.Should().BeEquivalentTo(["hello", "world"]);
    }

    [Fact]
    public void Tokenize_WithMixedCase_ShouldNormalizeToLowercase()
    {
        // Act
        var result = SearchUtilities.Tokenize("UPPER lower MiXeD").ToList();

        // Assert
        result.Should().BeEquivalentTo(["upper", "lower", "mixed"]);
    }

    [Fact]
    public void Tokenize_WithDuplicateTerms_ShouldReturnDistinct()
    {
        // Act
        var result = SearchUtilities.Tokenize("test Test TEST test").ToList();

        // Assert
        result.Should().HaveCount(1);
        result.Should().Contain("test");
    }

    [Fact]
    public void Tokenize_WithShortTerms_ShouldFilterOut()
    {
        // Act - terms less than 2 characters should be filtered
        var result = SearchUtilities.Tokenize("a b c to be or").ToList();

        // Assert
        result.Should().BeEquivalentTo(["to", "be", "or"]);
    }

    [Fact]
    public void Tokenize_WithSpecialCharacters_ShouldSplitOnNonAlphanumeric()
    {
        // Act
        var result = SearchUtilities.Tokenize("hello@world.com test_name file-path").ToList();

        // Assert
        result.Should().Contain("hello");
        result.Should().Contain("world");
        result.Should().Contain("com");
        result.Should().Contain("test_name");
        result.Should().Contain("file-path");
    }

    [Fact]
    public void Tokenize_WithNumbers_ShouldIncludeNumericTokens()
    {
        // Act
        var result = SearchUtilities.Tokenize("version 2.0 has 100 features").ToList();

        // Assert
        result.Should().Contain("version");
        result.Should().Contain("100");
        result.Should().Contain("features");
    }

    [Fact]
    public void Tokenize_WithEmptyString_ShouldReturnEmpty()
    {
        // Act
        var result = SearchUtilities.Tokenize("").ToList();

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public void Tokenize_WithOnlySpecialCharacters_ShouldReturnEmpty()
    {
        // Act
        var result = SearchUtilities.Tokenize("!@#$%^&*()").ToList();

        // Assert
        result.Should().BeEmpty();
    }

    [Theory]
    [InlineData("under_score", "under_score")]
    [InlineData("hyphen-ated", "hyphen-ated")]
    [InlineData("mixed_with-both", "mixed_with-both")]
    public void Tokenize_WithUnderscoreAndHyphen_ShouldPreserve(string input, string expected)
    {
        // Act
        var result = SearchUtilities.Tokenize(input).ToList();

        // Assert
        result.Should().Contain(expected);
    }

    #endregion

    #region ExtractSnippets Tests

    [Fact]
    public void ExtractSnippets_WithParagraphSeparatedContent_ShouldSplitOnDoubleNewline()
    {
        // Arrange
        var markdown = """
            This is the first paragraph with enough content to pass the minimum length requirement.

            This is the second paragraph which also has sufficient content to be considered a valid snippet.

            A third paragraph here with more than forty characters to meet the threshold.
            """;

        // Act
        var result = SearchUtilities.ExtractSnippets(markdown).ToList();

        // Assert
        result.Should().HaveCount(3);
    }

    [Fact]
    public void ExtractSnippets_WithShortParagraphs_ShouldFilterOut()
    {
        // Arrange
        var markdown = """
            Short.

            This is a much longer paragraph that definitely has more than forty characters.

            Tiny
            """;

        // Act
        var result = SearchUtilities.ExtractSnippets(markdown).ToList();

        // Assert
        result.Should().HaveCount(1);
        result.First().Should().Contain("much longer paragraph");
    }

    [Fact]
    public void ExtractSnippets_WithEmptyContent_ShouldReturnEmpty()
    {
        // Act
        var result = SearchUtilities.ExtractSnippets("").ToList();

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public void ExtractSnippets_WithOnlyWhitespace_ShouldReturnEmpty()
    {
        // Act
        var result = SearchUtilities.ExtractSnippets("   \n\n   \n\n   ").ToList();

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public void ExtractSnippets_WithMinimumLength_ShouldIncludeExactly40Chars()
    {
        // Arrange - exactly 40 characters
        var snippet = new string('a', 40);
        var markdown = $"{snippet}\n\n{new string('b', 39)}";

        // Act
        var result = SearchUtilities.ExtractSnippets(markdown).ToList();

        // Assert
        result.Should().HaveCount(1);
        result.First().Should().HaveLength(40);
    }

    #endregion

    #region ScoreSnippet Tests

    [Fact]
    public void ScoreSnippet_WithExactPhraseMatch_ShouldAddBonus()
    {
        // Arrange
        var snippet = "The quick brown fox jumps over the lazy dog";
        var phrase = "quick brown fox";
        var terms = new List<string> { "quick", "brown", "fox" };

        // Act
        var score = SearchUtilities.ScoreSnippet("file.md", snippet, phrase, terms);

        // Assert
        // 20 for phrase match + 3*3 for each term = 29 minimum
        score.Should().BeGreaterThanOrEqualTo(29);
    }

    [Fact]
    public void ScoreSnippet_WithTermsInFileName_ShouldAddFileNameBonus()
    {
        // Arrange
        var snippet = "Some content here";
        var phrase = "test";
        var terms = new List<string> { "test" };

        // Act
        var scoreWithTermInFile = SearchUtilities.ScoreSnippet("test-file.md", snippet, phrase, terms);
        var scoreWithoutTermInFile = SearchUtilities.ScoreSnippet("other-file.md", snippet, phrase, terms);

        // Assert
        scoreWithTermInFile.Should().BeGreaterThan(scoreWithoutTermInFile);
    }

    [Fact]
    public void ScoreSnippet_WithMultipleOccurrences_ShouldAddForEach()
    {
        // Arrange
        var snippet = "test test test - three occurrences of test";
        var phrase = "other";
        var terms = new List<string> { "test" };

        // Act
        var score = SearchUtilities.ScoreSnippet("file.md", snippet, phrase, terms);

        // Assert
        // 4 occurrences * 3 points each = 12
        score.Should().BeGreaterThanOrEqualTo(12);
    }

    [Fact]
    public void ScoreSnippet_WithNoMatches_ShouldReturnZero()
    {
        // Arrange
        var snippet = "Completely unrelated content";
        var phrase = "search term";
        var terms = new List<string> { "xyz", "abc" };

        // Act
        var score = SearchUtilities.ScoreSnippet("document.md", snippet, phrase, terms);

        // Assert
        score.Should().Be(0);
    }

    [Fact]
    public void ScoreSnippet_IsCaseInsensitive()
    {
        // Arrange
        var snippet = "TEST test Test";
        var phrase = "TEST";
        var terms = new List<string> { "test" };

        // Act
        var score = SearchUtilities.ScoreSnippet("file.md", snippet, phrase, terms);

        // Assert
        // Phrase match (20) + 3 term occurrences (9) = 29
        score.Should().BeGreaterThanOrEqualTo(29);
    }

    [Fact]
    public void ScoreSnippet_WithEmptyTerms_ShouldOnlyCountPhraseMatch()
    {
        // Arrange
        var snippet = "exact phrase here";
        var phrase = "exact phrase";
        var terms = new List<string>();

        // Act
        var score = SearchUtilities.ScoreSnippet("file.md", snippet, phrase, terms);

        // Assert
        score.Should().Be(20); // Only phrase match bonus
    }

    #endregion
}
