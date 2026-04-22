using CompanyBrain.Pruning;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace CompanyBrain.Tests.Pruning;

public sealed class SemanticChunkerTests
{
    #region Basic Chunking

    [Fact]
    public void Chunk_WithEmptyText_ShouldReturnEmpty()
    {
        var result = SemanticChunker.Chunk(string.Empty);

        result.Should().BeEmpty();
    }

    [Fact]
    public void Chunk_WithNullText_ShouldReturnEmpty()
    {
        var result = SemanticChunker.Chunk(null!);

        result.Should().BeEmpty();
    }

    [Fact]
    public void Chunk_WithSmallText_ShouldReturnSingleChunk()
    {
        var text = "This is a short paragraph that fits in one chunk.";

        var result = SemanticChunker.Chunk(text, maxSize: 600);

        result.Should().HaveCount(1);
        result[0].Should().Be(text);
    }

    [Fact]
    public void Chunk_WithMultipleParagraphs_ShouldSplitAtParagraphBoundaries()
    {
        var paragraph1 = new string('A', 200);
        var paragraph2 = new string('B', 200);
        var paragraph3 = new string('C', 200);
        var text = $"{paragraph1}\n\n{paragraph2}\n\n{paragraph3}";

        var result = SemanticChunker.Chunk(text, targetSize: 250, minSize: 50, maxSize: 300);

        result.Should().HaveCountGreaterThanOrEqualTo(2);
    }

    [Fact]
    public void Chunk_WithLongParagraph_ShouldSplitAtSentenceBoundaries()
    {
        var text = "First sentence about topic A. Second sentence expands on A. " +
                   "Third sentence introduces topic B. Fourth sentence about B. " +
                   "Fifth sentence wraps up the discussion. Sixth sentence concludes. " +
                   "Seventh sentence is additional padding to make this paragraph very long. " +
                   "Eighth sentence provides even more content for testing purposes. " +
                   "Ninth sentence continues the stream of test content here. " +
                   "Tenth sentence finishes the test paragraph with extra length.";

        var result = SemanticChunker.Chunk(text, targetSize: 100, minSize: 50, maxSize: 200);

        result.Should().HaveCountGreaterThan(1);
        result.Should().AllSatisfy(chunk => chunk.Length.Should().BeLessThanOrEqualTo(200));
    }

    [Fact]
    public void Chunk_ShouldMergeSmallTrailingChunks()
    {
        var paragraph1 = new string('A', 300);
        var paragraph2 = "Short.";
        var text = $"{paragraph1}\n\n{paragraph2}";

        var result = SemanticChunker.Chunk(text, targetSize: 400, minSize: 100, maxSize: 600);

        // The short trailing chunk should be merged with the previous one
        result.Should().HaveCount(1);
    }

    #endregion
}

public sealed class TfIdfScoringStrategyTests
{
    private readonly TfIdfScoringStrategy _strategy = new();

    #region Basic Scoring

    [Fact]
    public async Task ScoreAsync_WithEmptyChunks_ShouldReturnEmpty()
    {
        var result = await _strategy.ScoreAsync("query", [], CancellationToken.None);

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task ScoreAsync_WithMatchingChunk_ShouldScoreHigherThanNonMatching()
    {
        var chunks = new[]
        {
            "The weather forecast shows rain tomorrow and cloudy skies.",
            "Azure deployment pipelines use continuous integration patterns.",
            "The weather in Seattle includes frequent rain and overcast days."
        };

        var result = await _strategy.ScoreAsync("weather rain forecast", chunks, CancellationToken.None);

        result.Should().HaveCount(3);

        var weatherChunks = result.Where(c => c.Text.Contains("weather")).ToList();
        var azureChunk = result.First(c => c.Text.Contains("Azure"));

        weatherChunks.Should().AllSatisfy(c => c.Score.Should().BeGreaterThan(azureChunk.Score));
    }

    [Fact]
    public async Task ScoreAsync_WithEmptyQuery_ShouldReturnZeroScores()
    {
        var chunks = new[] { "Some text content." };

        var result = await _strategy.ScoreAsync(string.Empty, chunks, CancellationToken.None);

        result.Should().HaveCount(1);
        result[0].Score.Should().Be(0.0);
    }

    [Fact]
    public async Task ScoreAsync_ShouldPreserveChunkIndices()
    {
        var chunks = new[] { "First chunk.", "Second chunk.", "Third chunk." };

        var result = await _strategy.ScoreAsync("chunk", chunks, CancellationToken.None);

        result[0].Index.Should().Be(0);
        result[1].Index.Should().Be(1);
        result[2].Index.Should().Be(2);
    }

    [Fact]
    public async Task ScoreAsync_WithExactMatch_ShouldReturnHighScore()
    {
        var chunks = new[]
        {
            "MediatR pattern implementation guide for CQRS.",
            "Unrelated content about cooking recipes and kitchen tools."
        };

        var result = await _strategy.ScoreAsync("MediatR CQRS pattern", chunks, CancellationToken.None);

        result[0].Score.Should().BeGreaterThan(0.5);
        result[1].Score.Should().BeLessThan(result[0].Score);
    }

    [Fact]
    public async Task ScoreAsync_WithCancellation_ShouldThrow()
    {
        var chunks = new[] { "Test content." };
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        Func<Task> act = async () => await _strategy.ScoreAsync("query", chunks, cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    #endregion
}

public sealed class ContextBudgetManagerTests
{
    #region FitsWithinBudget

    [Fact]
    public void FitsWithinBudget_WithSmallText_ShouldReturnTrue()
    {
        var text = new string('x', 100); // ~25 tokens

        ContextBudgetManager.FitsWithinBudget(text, 2000).Should().BeTrue();
    }

    [Fact]
    public void FitsWithinBudget_WithLargeText_ShouldReturnFalse()
    {
        var text = new string('x', 10_000); // ~2500 tokens

        ContextBudgetManager.FitsWithinBudget(text, 2000).Should().BeFalse();
    }

    [Fact]
    public void FitsWithinBudget_AtExactBudget_ShouldReturnTrue()
    {
        var text = new string('x', 8000); // exactly 2000 tokens

        ContextBudgetManager.FitsWithinBudget(text, 2000).Should().BeTrue();
    }

    #endregion

    #region SelectChunks

    [Fact]
    public void SelectChunks_ShouldReturnTopScoringChunks()
    {
        var chunks = new ScoredChunk[]
        {
            new("Low relevance chunk", 0, 0.1),
            new("High relevance chunk", 1, 0.9),
            new("Medium relevance chunk", 2, 0.5)
        };

        var result = ContextBudgetManager.SelectChunks(chunks, maxChunks: 2, tokenBudget: 2000, relevanceThreshold: 0.3);

        result.Should().HaveCount(2);
        result.Should().NotContain(c => c.Score == 0.1);
    }

    [Fact]
    public void SelectChunks_ShouldRespectTokenBudget()
    {
        var longText = new string('x', 4000); // ~1000 tokens each
        var chunks = new ScoredChunk[]
        {
            new(longText, 0, 0.9),
            new(longText, 1, 0.8),
            new(longText, 2, 0.7)
        };

        var result = ContextBudgetManager.SelectChunks(chunks, maxChunks: 3, tokenBudget: 1500, relevanceThreshold: 0.0);

        result.Should().HaveCount(1);
    }

    [Fact]
    public void SelectChunks_ShouldPreserveDocumentOrder()
    {
        var chunks = new ScoredChunk[]
        {
            new("First", 0, 0.5),
            new("Second", 1, 0.9),
            new("Third", 2, 0.7)
        };

        var result = ContextBudgetManager.SelectChunks(chunks, maxChunks: 3, tokenBudget: 2000, relevanceThreshold: 0.3);

        result.Should().BeInAscendingOrder(c => c.Index);
    }

    [Fact]
    public void SelectChunks_BelowThreshold_ShouldBeExcluded()
    {
        var chunks = new ScoredChunk[]
        {
            new("Below threshold", 0, 0.1),
            new("Above threshold", 1, 0.5)
        };

        var result = ContextBudgetManager.SelectChunks(chunks, maxChunks: 3, tokenBudget: 2000, relevanceThreshold: 0.3);

        result.Should().HaveCount(1);
        result[0].Text.Should().Be("Above threshold");
    }

    #endregion

    #region EstimateTokens

    [Fact]
    public void EstimateTokens_ShouldRoundUpToNearestToken()
    {
        ContextBudgetManager.EstimateTokens("ab").Should().Be(1); // 2 chars / 4 = 0.5 → 1
        ContextBudgetManager.EstimateTokens("abcd").Should().Be(1); // 4 chars / 4 = 1
        ContextBudgetManager.EstimateTokens("abcde").Should().Be(2); // 5 chars / 4 = 1.25 → 2
    }

    #endregion
}

public sealed class IntelligentPruningServiceTests
{
    private readonly TfIdfScoringStrategy _strategy = new();
    private readonly ILogger<IntelligentPruningService> _logger =
        Substitute.For<ILogger<IntelligentPruningService>>();

    #region PassThrough Scenarios

    [Fact]
    public async Task PruneAsync_WhenDisabled_ShouldReturnOriginalText()
    {
        var config = new PruningConfiguration { Enabled = false };
        var service = CreateService(config);

        var result = await service.PruneAsync("Some long text content here", "query");

        result.WasPruned.Should().BeFalse();
        result.Text.Should().Be("Some long text content here");
    }

    [Fact]
    public async Task PruneAsync_WithEmptyText_ShouldReturnEmpty()
    {
        var service = CreateService();

        var result = await service.PruneAsync(string.Empty, "query");

        result.WasPruned.Should().BeFalse();
        result.Text.Should().BeEmpty();
    }

    [Fact]
    public async Task PruneAsync_WithSmallDocument_ShouldNotPrune()
    {
        var config = new PruningConfiguration { TokenBudget = 2000 };
        var service = CreateService(config);
        var smallText = "This is a small document that easily fits within the token budget.";

        var result = await service.PruneAsync(smallText, "document");

        result.WasPruned.Should().BeFalse();
        result.Text.Should().Be(smallText);
    }

    #endregion

    #region Pruning Scenarios

    [Fact]
    public async Task PruneAsync_WithLargeDocument_ShouldPrune()
    {
        var config = new PruningConfiguration
        {
            TokenBudget = 100,
            MaxChunks = 2,
            RelevanceThreshold = 0.0,
            ChunkTargetSize = 200,
            ChunkMinSize = 50,
            ChunkMaxSize = 300
        };
        var service = CreateService(config);

        var paragraphs = Enumerable.Range(1, 20)
            .Select(i => $"Paragraph {i} contains information about topic {(i % 3 == 0 ? "Azure deployment" : "cooking recipes")}. " +
                         $"This paragraph has enough text to be meaningful for chunking purposes and testing.")
            .ToArray();
        var largeText = string.Join("\n\n", paragraphs);

        var result = await service.PruneAsync(largeText, "Azure deployment");

        result.WasPruned.Should().BeTrue();
        result.PrunedTokens.Should().BeLessThan(result.OriginalTokens);
        result.ChunksSelected.Should().BeGreaterThan(0);
        result.ChunksSelected.Should().BeLessThanOrEqualTo(config.MaxChunks);
    }

    [Fact]
    public async Task PruneAsync_ShouldPreferRelevantChunks()
    {
        var config = new PruningConfiguration
        {
            TokenBudget = 50,
            MaxChunks = 1,
            RelevanceThreshold = 0.0,
            ChunkTargetSize = 200,
            ChunkMinSize = 50,
            ChunkMaxSize = 300
        };
        var service = CreateService(config);

        var text = string.Join("\n\n",
            "Cooking recipes involve mixing ingredients in a bowl and baking in an oven at proper temperature.",
            "The Azure deployment pipeline uses continuous integration and continuous delivery patterns.",
            "The garden plants need regular watering and sunlight exposure for healthy growth.");

        var result = await service.PruneAsync(text, "Azure deployment pipeline");

        result.WasPruned.Should().BeTrue();
        result.Text.Should().Contain("Azure");
    }

    [Fact]
    public async Task PruneAsync_WhenNoChunksMeetThreshold_ShouldFallbackToTopChunk()
    {
        var config = new PruningConfiguration
        {
            TokenBudget = 10,
            MaxChunks = 1,
            RelevanceThreshold = 0.99,
            ChunkTargetSize = 200,
            ChunkMinSize = 50,
            ChunkMaxSize = 300
        };
        var service = CreateService(config);

        var text = string.Join("\n\n",
            new string('A', 200),
            new string('B', 200),
            new string('C', 200));

        var result = await service.PruneAsync(text, "completely unrelated query");

        result.WasPruned.Should().BeTrue();
        result.ChunksSelected.Should().Be(1);
    }

    #endregion

    #region Token Estimation

    [Fact]
    public async Task PruneAsync_ShouldReportCorrectTokenCounts()
    {
        var config = new PruningConfiguration
        {
            TokenBudget = 50,
            MaxChunks = 1,
            RelevanceThreshold = 0.0,
            ChunkTargetSize = 200,
            ChunkMinSize = 50,
            ChunkMaxSize = 300
        };
        var service = CreateService(config);

        var text = string.Join("\n\n",
            Enumerable.Range(1, 10).Select(i => new string((char)('A' + i), 200)));

        var result = await service.PruneAsync(text, "query");

        result.OriginalTokens.Should().BeGreaterThan(0);
        result.PrunedTokens.Should().BeLessThanOrEqualTo(result.OriginalTokens);
    }

    #endregion

    private IntelligentPruningService CreateService(PruningConfiguration? config = null)
    {
        return new IntelligentPruningService(
            _strategy,
            config ?? new PruningConfiguration(),
            _logger);
    }
}

public sealed class PruningConfigurationTests
{
    [Fact]
    public void Defaults_ShouldHaveReasonableValues()
    {
        var config = new PruningConfiguration();

        config.Enabled.Should().BeTrue();
        config.RelevanceThreshold.Should().BeInRange(0.0, 1.0);
        config.MaxChunks.Should().BeGreaterThan(0);
        config.TokenBudget.Should().BeGreaterThan(0);
        config.ChunkTargetSize.Should().BeGreaterThan(config.ChunkMinSize);
        config.ChunkMaxSize.Should().BeGreaterThan(config.ChunkTargetSize);
    }
}
