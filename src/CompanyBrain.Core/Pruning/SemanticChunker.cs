namespace CompanyBrain.Pruning;

/// <summary>
/// Splits a document into semantically meaningful chunks at paragraph and sentence boundaries.
/// Prefers splitting at double-newlines, falls back to sentence-ending punctuation.
/// </summary>
public static class SemanticChunker
{
    private static readonly char[] SentenceEnders = ['.', '!', '?'];

    /// <summary>
    /// Splits <paramref name="text"/> into chunks respecting the given size constraints.
    /// </summary>
    public static IReadOnlyList<string> Chunk(
        string text,
        int targetSize = 400,
        int minSize = 100,
        int maxSize = 600)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return [];
        }

        if (text.Length <= maxSize)
        {
            return [text];
        }

        var paragraphs = SplitParagraphs(text);
        var chunks = new List<string>();
        var buffer = new System.Text.StringBuilder(targetSize);

        foreach (var paragraph in paragraphs)
        {
            if (buffer.Length + paragraph.Length + 2 > maxSize && buffer.Length >= minSize)
            {
                chunks.Add(buffer.ToString().Trim());
                buffer.Clear();
            }

            if (paragraph.Length > maxSize)
            {
                FlushBuffer(buffer, chunks, minSize);
                SplitLongParagraph(paragraph, targetSize, maxSize, chunks);
                continue;
            }

            if (buffer.Length > 0)
            {
                buffer.Append("\n\n");
            }

            buffer.Append(paragraph);

            if (buffer.Length < targetSize)
            {
                continue;
            }

            chunks.Add(buffer.ToString().Trim());
            buffer.Clear();
        }

        FlushBuffer(buffer, chunks, minSize: 0);
        MergeSmallTrailingChunk(chunks, minSize, maxSize);

        return chunks;
    }

    private static List<string> SplitParagraphs(string text)
    {
        var parts = text.Split(["\n\n", "\r\n\r\n"], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var result = new List<string>(parts.Length);

        foreach (var part in parts)
        {
            if (part.Length > 0)
            {
                result.Add(part);
            }
        }

        return result;
    }

    private static void SplitLongParagraph(
        string paragraph,
        int targetSize,
        int maxSize,
        List<string> chunks)
    {
        var start = 0;

        while (start < paragraph.Length)
        {
            var remaining = paragraph.Length - start;

            if (remaining <= maxSize)
            {
                chunks.Add(paragraph[start..].Trim());
                break;
            }

            var splitAt = FindSentenceBoundary(paragraph, start, targetSize, maxSize);
            var chunk = paragraph[start..splitAt].Trim();

            if (chunk.Length > 0)
            {
                chunks.Add(chunk);
            }

            start = splitAt;
        }
    }

    private static int FindSentenceBoundary(string text, int start, int targetSize, int maxSize)
    {
        var idealEnd = Math.Min(start + targetSize, text.Length);
        var hardEnd = Math.Min(start + maxSize, text.Length);

        // Search for sentence-ending punctuation near the target size
        for (var i = idealEnd; i > start + (targetSize / 2); i--)
        {
            if (IsSentenceEnd(text, i))
            {
                return i + 1;
            }
        }

        // Fall back to searching forward up to max size
        for (var i = idealEnd; i < hardEnd; i++)
        {
            if (IsSentenceEnd(text, i))
            {
                return i + 1;
            }
        }

        // Last resort: split at max size
        return hardEnd;
    }

    private static bool IsSentenceEnd(string text, int index)
    {
        if (index >= text.Length)
        {
            return false;
        }

        if (Array.IndexOf(SentenceEnders, text[index]) < 0)
        {
            return false;
        }

        // Ensure the period is followed by whitespace or end-of-string
        var next = index + 1;
        return next >= text.Length || char.IsWhiteSpace(text[next]);
    }

    private static void FlushBuffer(System.Text.StringBuilder buffer, List<string> chunks, int minSize)
    {
        if (buffer.Length < minSize)
        {
            return;
        }

        chunks.Add(buffer.ToString().Trim());
        buffer.Clear();
    }

    private static void MergeSmallTrailingChunk(List<string> chunks, int minSize, int maxSize)
    {
        if (chunks.Count < 2)
        {
            return;
        }

        var last = chunks[^1];
        if (last.Length >= minSize)
        {
            return;
        }

        var prev = chunks[^2];
        if (prev.Length + last.Length + 2 <= maxSize)
        {
            chunks[^2] = prev + "\n\n" + last;
            chunks.RemoveAt(chunks.Count - 1);
        }
    }
}
