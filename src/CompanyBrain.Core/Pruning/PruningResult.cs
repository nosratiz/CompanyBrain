namespace CompanyBrain.Pruning;

/// <summary>
/// Result from the pruning service indicating what was done with the input text.
/// </summary>
/// <param name="Text">The (possibly pruned) output text.</param>
/// <param name="WasPruned">True when the text was chunked and selectively included.</param>
/// <param name="OriginalTokens">Estimated token count of the original text.</param>
/// <param name="PrunedTokens">Estimated token count of the pruned output.</param>
/// <param name="ChunksEvaluated">Number of chunks scored.</param>
/// <param name="ChunksSelected">Number of chunks included in the output.</param>
public readonly record struct PruningResult(
    string Text,
    bool WasPruned,
    int OriginalTokens,
    int PrunedTokens,
    int ChunksEvaluated,
    int ChunksSelected);
