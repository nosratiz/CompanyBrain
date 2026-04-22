using CompanyBrain.Pruning;

namespace CompanyBrain.Dashboard.Services;

/// <summary>
/// A single recorded pruning event capturing what happened to a tool call's output.
/// </summary>
public sealed class PruningEvent
{
    public required string ToolName { get; init; }
    public required string Query { get; init; }
    public required string SourceAttribution { get; init; }
    public required DateTimeOffset Timestamp { get; init; }
    public required int OriginalTokens { get; init; }
    public required int PrunedTokens { get; init; }
    public required int ChunksEvaluated { get; init; }
    public required int ChunksSelected { get; init; }
    public required bool WasPruned { get; init; }
    public required bool PiiDetected { get; init; }
    public required IReadOnlyList<SnippetDetail> Snippets { get; init; }

    public int TokensSaved => OriginalTokens - PrunedTokens;
}

/// <summary>
/// A single snippet selected by the pruning engine, with its score and redacted content.
/// </summary>
public sealed class SnippetDetail
{
    public required string Text { get; init; }
    public required string RedactedText { get; init; }
    public required double SimilarityScore { get; init; }
    public required int Index { get; init; }
    public required string Source { get; init; }
}
