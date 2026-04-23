namespace CompanyBrain.Search.Vector;

/// <summary>
/// One row returned from a sqlite-vec top-K cosine search.
/// </summary>
/// <param name="ResourceUri">The MCP-style resource URI (e.g., <c>knowledge://General/foo.md</c>).</param>
/// <param name="CollectionId">The owning knowledge collection.</param>
/// <param name="RedactedSnippet">PII-masked snippet stored alongside the vector.</param>
/// <param name="Distance">Cosine distance (lower is better).</param>
public sealed record VectorSearchResult(
    string ResourceUri,
    string CollectionId,
    string RedactedSnippet,
    double Distance);
