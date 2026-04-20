using System.Security.Cryptography;

namespace CompanyBrain.Dashboard.Features.DeepClean;

/// <summary>
/// Securely erases file contents before deletion to prevent forensic recovery.
/// Overwrites with zeros (and optionally random data) before unlinking.
/// Thread-safe and Native AOT compatible — no reflection.
/// </summary>
internal static class SecureEraser
{
    private const int BufferSize = 81_920; // 80 KB — matches default FileStream buffer

    /// <summary>
    /// Overwrites <paramref name="filePath"/> with zero-fill (and optional additional passes),
    /// then deletes the file. Retries on <see cref="IOException"/> (file locks).
    /// </summary>
    /// <param name="filePath">Absolute path to the file to securely erase.</param>
    /// <param name="passes">Number of overwrite passes (1 = zero-fill, 3 = DoD-style).</param>
    /// <param name="retryCount">Max retry attempts on file-lock failures.</param>
    /// <param name="retryDelay">Delay between retries.</param>
    /// <param name="logger">Logger for diagnostics (no sensitive file names logged).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if the file was successfully erased and deleted.</returns>
    public static async Task<bool> SecureDeleteAsync(
        string filePath,
        int passes,
        int retryCount,
        TimeSpan retryDelay,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        for (var attempt = 0; attempt <= retryCount; attempt++)
        {
            try
            {
                if (!File.Exists(filePath))
                {
                    logger.LogDebug("Secure delete skipped — target no longer exists");
                    return false;
                }

                await OverwriteFileAsync(filePath, passes, cancellationToken);
                File.Delete(filePath);
                logger.LogDebug("Secure erasure completed ({Passes} pass(es))", passes);
                return true;
            }
            catch (IOException) when (attempt < retryCount)
            {
                logger.LogDebug("File locked, retrying in {Delay}ms (attempt {Attempt}/{Max})",
                    retryDelay.TotalMilliseconds, attempt + 1, retryCount);
                await Task.Delay(retryDelay, cancellationToken);
            }
            catch (UnauthorizedAccessException ex)
            {
                logger.LogWarning(ex, "Secure delete denied — insufficient permissions");
                return false;
            }
        }

        logger.LogWarning("Secure delete failed after {Retries} retries — file still locked", retryCount);
        return false;
    }

    /// <summary>
    /// Overwrites the file content with <paramref name="passes"/> rounds of data
    /// without expanding or truncating the file.
    /// </summary>
    private static async Task OverwriteFileAsync(string filePath, int passes, CancellationToken cancellationToken)
    {
        var fileLength = new FileInfo(filePath).Length;
        if (fileLength == 0)
            return;

        var buffer = new byte[Math.Min(fileLength, BufferSize)];

        for (var pass = 0; pass < passes; pass++)
        {
            FillBuffer(buffer, pass, passes);

            await using var stream = new FileStream(
                filePath,
                FileMode.Open,
                FileAccess.Write,
                FileShare.None,
                bufferSize: buffer.Length,
                useAsync: true);

            var remaining = fileLength;
            while (remaining > 0)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var chunk = (int)Math.Min(remaining, buffer.Length);
                await stream.WriteAsync(buffer.AsMemory(0, chunk), cancellationToken);
                remaining -= chunk;
            }

            await stream.FlushAsync(cancellationToken);
        }
    }

    /// <summary>
    /// Fills the buffer with the appropriate pattern for the given pass:
    /// Pass 0 → zeros, Pass 1 → 0xFF, Pass 2+ → cryptographic random.
    /// Single-pass mode always uses zeros.
    /// </summary>
    private static void FillBuffer(byte[] buffer, int passIndex, int totalPasses)
    {
        if (totalPasses == 1 || passIndex == 0)
        {
            Array.Clear(buffer);
        }
        else if (passIndex == 1)
        {
            Array.Fill(buffer, (byte)0xFF);
        }
        else
        {
            RandomNumberGenerator.Fill(buffer);
        }
    }
}
