namespace CompanyBrain.Pruning;

/// <summary>
/// A scored text fragment produced by the semantic chunker and relevance engine.
/// </summary>
/// <param name="Text">The chunk text content.</param>
/// <param name="Index">Original position index in the source document.</param>
/// <param name="Score">Relevance score from the scoring strategy (0.0–1.0).</param>
public readonly record struct ScoredChunk(string Text, int Index, double Score);
