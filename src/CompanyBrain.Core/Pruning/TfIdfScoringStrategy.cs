using System.Text.RegularExpressions;

namespace CompanyBrain.Pruning;

/// <summary>
/// TF-IDF based relevance scorer that works without GPU or ML model downloads.
/// Uses term frequency–inverse document frequency cosine similarity between
/// the query and each chunk. Fully AOT-compatible.
/// </summary>
public sealed partial class TfIdfScoringStrategy : IRelevanceScoringStrategy
{
    [GeneratedRegex(@"[A-Za-z0-9][A-Za-z0-9_-]*", RegexOptions.CultureInvariant)]
    private static partial Regex TokenRegex();

    public ValueTask<IReadOnlyList<ScoredChunk>> ScoreAsync(
        string query,
        IReadOnlyList<string> chunks,
        CancellationToken cancellationToken = default)
    {
        if (chunks.Count == 0)
        {
            return new ValueTask<IReadOnlyList<ScoredChunk>>(Array.Empty<ScoredChunk>());
        }

        var queryTerms = Tokenize(query);

        if (queryTerms.Count == 0)
        {
            return new ValueTask<IReadOnlyList<ScoredChunk>>(
                CreateUniformScores(chunks));
        }

        var chunkTermFrequencies = new List<Dictionary<string, int>>(chunks.Count);
        foreach (var chunk in chunks)
        {
            chunkTermFrequencies.Add(ComputeTermFrequency(Tokenize(chunk)));
        }

        var idf = ComputeIdf(queryTerms, chunkTermFrequencies);
        var queryTfIdf = ComputeTfIdfVector(ComputeTermFrequency(queryTerms), idf);

        var results = new ScoredChunk[chunks.Count];
        for (var i = 0; i < chunks.Count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var chunkTfIdf = ComputeTfIdfVector(chunkTermFrequencies[i], idf);
            var similarity = CosineSimilarity(queryTfIdf, chunkTfIdf);

            results[i] = new ScoredChunk(chunks[i], i, similarity);
        }

        return new ValueTask<IReadOnlyList<ScoredChunk>>(results);
    }

    private static IReadOnlyList<string> Tokenize(string text)
    {
        var matches = TokenRegex().Matches(text);
        var tokens = new List<string>(matches.Count);

        foreach (Match match in matches)
        {
            var lower = match.Value.ToLowerInvariant();
            if (lower.Length >= 2)
            {
                tokens.Add(lower);
            }
        }

        return tokens;
    }

    private static Dictionary<string, int> ComputeTermFrequency(IReadOnlyList<string> tokens)
    {
        var freq = new Dictionary<string, int>(StringComparer.Ordinal);

        foreach (var token in tokens)
        {
            freq[token] = freq.TryGetValue(token, out var count) ? count + 1 : 1;
        }

        return freq;
    }

    private static Dictionary<string, double> ComputeIdf(
        IReadOnlyList<string> queryTerms,
        List<Dictionary<string, int>> chunkTermFrequencies)
    {
        var docCount = chunkTermFrequencies.Count;
        var idf = new Dictionary<string, double>(StringComparer.Ordinal);
        var uniqueTerms = new HashSet<string>(queryTerms, StringComparer.Ordinal);

        foreach (var tf in chunkTermFrequencies)
        {
            foreach (var key in tf.Keys)
            {
                uniqueTerms.Add(key);
            }
        }

        foreach (var term in uniqueTerms)
        {
            var docFreq = 0;
            foreach (var tf in chunkTermFrequencies)
            {
                if (tf.ContainsKey(term))
                {
                    docFreq++;
                }
            }

            // Standard IDF with +1 smoothing to avoid division by zero
            idf[term] = Math.Log((docCount + 1.0) / (docFreq + 1.0)) + 1.0;
        }

        return idf;
    }

    private static Dictionary<string, double> ComputeTfIdfVector(
        Dictionary<string, int> termFrequency,
        Dictionary<string, double> idf)
    {
        var vector = new Dictionary<string, double>(StringComparer.Ordinal);

        foreach (var (term, freq) in termFrequency)
        {
            if (idf.TryGetValue(term, out var idfValue))
            {
                vector[term] = freq * idfValue;
            }
        }

        return vector;
    }

    private static double CosineSimilarity(
        Dictionary<string, double> vectorA,
        Dictionary<string, double> vectorB)
    {
        var dotProduct = 0.0;
        var magnitudeA = 0.0;
        var magnitudeB = 0.0;

        foreach (var (term, valueA) in vectorA)
        {
            magnitudeA += valueA * valueA;

            if (vectorB.TryGetValue(term, out var valueB))
            {
                dotProduct += valueA * valueB;
            }
        }

        foreach (var (_, valueB) in vectorB)
        {
            magnitudeB += valueB * valueB;
        }

        var magnitude = Math.Sqrt(magnitudeA) * Math.Sqrt(magnitudeB);

        return magnitude == 0 ? 0.0 : dotProduct / magnitude;
    }

    private static IReadOnlyList<ScoredChunk> CreateUniformScores(IReadOnlyList<string> chunks)
    {
        var results = new ScoredChunk[chunks.Count];
        for (var i = 0; i < chunks.Count; i++)
        {
            results[i] = new ScoredChunk(chunks[i], i, 0.0);
        }

        return results;
    }
}
