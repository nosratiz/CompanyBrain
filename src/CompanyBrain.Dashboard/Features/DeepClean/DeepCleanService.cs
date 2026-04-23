using CompanyBrain.Dashboard.Data.Audit;
using CompanyBrain.Dashboard.Services.Audit;
using Microsoft.Extensions.Options;

namespace CompanyBrain.Dashboard.Features.DeepClean;

/// <summary>
/// Background service that maintains DeepRoot's local-first data stores.
/// Runs on a configurable cycle (default 24 h) using <see cref="PeriodicTimer"/>
/// for modern async scheduling — no <c>System.Timers.Timer</c>, no reflection.
///
/// Responsibilities per cycle:
///   1. Orphan cleanup  — removes index/metadata records whose backing files are gone.
///   2. SQLite VACUUM + ANALYZE  — defragments databases and refreshes query stats.
///   3. Temp fragment scrubbing — securely overwrites transient plaintext chunks.
///   4. Storage guardrail — enforces a configurable quota via LRU eviction.
/// </summary>
internal sealed class DeepCleanService(
    DeepCleanRepository repository,
    IOptions<DeepCleanOptions> options,
    IServiceScopeFactory scopeFactory,
    ILogger<DeepCleanService> logger) : BackgroundService
{
    private readonly DeepCleanOptions _options = options.Value;

    // ──────────────────────────── Main Loop ────────────────────────────

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation(
            "DeepClean service started — quota {QuotaMb} MB, cycle every {Hours}h",
            _options.QuotaMb, _options.CycleInterval.TotalHours);

        if (!await WaitForInitialDelayAsync(stoppingToken))
            return;

        using var timer = new PeriodicTimer(_options.CycleInterval);

        var firstRun = true;
        while (!stoppingToken.IsCancellationRequested)
        {
            if (!firstRun && !await WaitForNextTickAsync(timer, stoppingToken))
                break;

            firstRun = false;
            await RunCycleAsync(stoppingToken);
        }

        logger.LogInformation("DeepClean service stopped");
    }

    private static async Task<bool> WaitForInitialDelayAsync(CancellationToken ct)
    {
        try
        {
            await Task.Delay(TimeSpan.FromSeconds(30), ct);
            return true;
        }
        catch (OperationCanceledException)
        {
            return false;
        }
    }

    private static async Task<bool> WaitForNextTickAsync(PeriodicTimer timer, CancellationToken ct)
    {
        try
        {
            return await timer.WaitForNextTickAsync(ct);
        }
        catch (OperationCanceledException)
        {
            return false;
        }
    }

    private async Task RunCycleAsync(CancellationToken stoppingToken)
    {
        try
        {
            logger.LogInformation("DeepClean cycle starting");
            var summary = new CycleSummary();

            await Phase1_OrphanCleanupAsync(summary, stoppingToken);
            await Phase2_SqliteMaintenanceAsync(stoppingToken);
            await Phase3_TempFragmentScrubAsync(summary, stoppingToken);
            await Phase4_StorageGuardrailAsync(summary, stoppingToken);

            logger.LogInformation(
                "DeepClean cycle complete — orphans removed: {Orphans}, " +
                "fragments scrubbed: {Fragments}, LRU evicted: {Evicted} ({EvictedMb:F1} MB)",
                summary.OrphansRemoved,
                summary.FragmentsScrubbed,
                summary.LruEvicted,
                summary.LruEvictedBytes / (1024.0 * 1024.0));
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Graceful shutdown — no log needed
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "DeepClean cycle failed");
        }
    }

    // ──────────────────────────── Phase 1: Orphan Cleanup ────────────────────────────

    /// <summary>
    /// Removes index/metadata records whose backing files no longer exist on disk.
    /// Covers SharePoint synced files and document-tenant assignments.
    /// </summary>
    private async Task Phase1_OrphanCleanupAsync(CycleSummary summary, CancellationToken ct)
    {
        try
        {
            var spOrphans = await repository.RemoveOrphanedSharePointRecordsAsync(ct);
            var assignOrphans = await repository.RemoveOrphanedAssignmentsAsync(ct);

            summary.OrphansRemoved = spOrphans + assignOrphans;

            if (summary.OrphansRemoved > 0)
            {
                logger.LogInformation(
                    "Orphan cleanup: {SpOrphans} SharePoint records, {AssignOrphans} assignment records",
                    spOrphans, assignOrphans);
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Orphan cleanup phase failed");
        }
    }

    // ──────────────────────────── Phase 2: SQLite Maintenance ────────────────────────────

    /// <summary>
    /// Runs VACUUM and ANALYZE on all SQLite databases.
    /// WAL mode ensures this doesn't block concurrent reads (MCP tool-calling thread).
    /// </summary>
    private async Task Phase2_SqliteMaintenanceAsync(CancellationToken ct)
    {
        try
        {
            await repository.RunMaintenanceAsync(ct);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "SQLite maintenance phase failed");
        }
    }

    // ──────────────────────────── Phase 3: Temp Fragment Scrubbing ────────────────────────────

    /// <summary>
    /// Securely erases temporary plaintext fragments produced during indexing.
    /// Scans known temp directories for leftover .tmp / .chunk files and zero-fills
    /// them before deletion, preventing forensic recovery of unencrypted text.
    /// </summary>
    private async Task Phase3_TempFragmentScrubAsync(CycleSummary summary, CancellationToken ct)
    {
        try
        {
            var tempDirs = GetTempFragmentDirectories();

            foreach (var dir in tempDirs)
            {
                if (!Directory.Exists(dir))
                    continue;

                var tempFiles = Directory.EnumerateFiles(dir, "*", SearchOption.AllDirectories)
                    .Where(IsTempFragment);

                foreach (var file in tempFiles)
                {
                    ct.ThrowIfCancellationRequested();

                    var erased = await SecureEraser.SecureDeleteAsync(
                        file,
                        _options.SecureDeletePasses,
                        _options.FileRetryCount,
                        _options.FileRetryDelay,
                        logger,
                        ct);

                    if (erased)
                        summary.FragmentsScrubbed++;
                }
            }

            if (summary.FragmentsScrubbed > 0)
                logger.LogInformation("Scrubbed {Count} temp fragment(s)", summary.FragmentsScrubbed);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Temp fragment scrub phase failed");
        }
    }

    // ──────────────────────────── Phase 4: Storage Guardrail ────────────────────────────

    /// <summary>
    /// Enforces the storage quota. Calculates the total index directory size
    /// (SQLite database files + local synced file blobs). When the total exceeds
    /// <see cref="DeepCleanOptions.QuotaMb"/>, evicts the least-recently-used
    /// records until usage drops below <c>QuotaMb × PurgeTargetRatio</c> (default 80%).
    ///
    /// The 500 MB guardrail is calculated as:
    ///   TotalSize = Σ(SQLite .db files) + Σ(synced local files in Index directories)
    ///   This includes sharepoint_sync.db, document_assignments.db, confluence_sync.db,
    ///   plus all blob files in SharePoint sync folders.
    /// </summary>
    private async Task Phase4_StorageGuardrailAsync(CycleSummary summary, CancellationToken ct)
    {
        try
        {
            var quotaBytes = _options.QuotaMb * 1024L * 1024L;
            var targetBytes = (long)(quotaBytes * _options.PurgeTargetRatio);
            var currentBytes = CalculateTotalIndexSize();

            if (currentBytes <= quotaBytes)
            {
                logger.LogDebug(
                    "Storage within quota: {CurrentMb:F1} MB / {QuotaMb} MB",
                    currentBytes / (1024.0 * 1024.0), _options.QuotaMb);
                return;
            }

            logger.LogWarning(
                "Storage quota exceeded: {CurrentMb:F1} MB / {QuotaMb} MB — starting LRU eviction to {TargetMb:F1} MB",
                currentBytes / (1024.0 * 1024.0), _options.QuotaMb, targetBytes / (1024.0 * 1024.0));

            var bytesToFree = currentBytes - targetBytes;
            await EvictLruRecordsAsync(summary, bytesToFree, ct);

            logger.LogInformation(
                "LRU eviction complete: freed {FreedMb:F1} MB, evicted {Count} records",
                summary.LruEvictedBytes / (1024.0 * 1024.0), summary.LruEvicted);

            await WriteEvictionAuditAsync(summary.LruEvicted, summary.LruEvictedBytes, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Storage guardrail phase failed");
        }
    }

    private async Task EvictLruRecordsAsync(CycleSummary summary, long bytesToFree, CancellationToken ct)
    {
        var freedBytes = 0L;
        var evictedIds = new List<int>();
        const int batchSize = 100;

        while (freedBytes < bytesToFree)
        {
            ct.ThrowIfCancellationRequested();

            var candidates = await repository.GetLruCandidatesAsync(batchSize, ct);
            if (candidates.Count == 0)
                break;

            foreach (var candidate in candidates)
            {
                if (freedBytes >= bytesToFree)
                    break;

                if (File.Exists(candidate.LocalPath))
                {
                    await SecureEraser.SecureDeleteAsync(
                        candidate.LocalPath,
                        _options.SecureDeletePasses,
                        _options.FileRetryCount,
                        _options.FileRetryDelay,
                        logger,
                        ct);
                }

                evictedIds.Add(candidate.Id);
                freedBytes += candidate.SizeBytes;
            }

            await repository.DeleteSharePointRecordsByIdAsync(evictedIds, ct);
            summary.LruEvicted += evictedIds.Count;
            summary.LruEvictedBytes += freedBytes;
            evictedIds.Clear();
        }
    }

    // ──────────────────────────── Helpers ────────────────────────────

    private async Task WriteEvictionAuditAsync(int count, long bytes, CancellationToken ct)
    {
        try
        {
            using var scope = scopeFactory.CreateScope();
            var audit = scope.ServiceProvider.GetRequiredService<IAuditService>();
            await audit.LogAsync(AuditEventType.DocumentDeleted, new AuditEntry(
                ActorId: "system",
                ResourceType: "Document",
                ResourceName: "LRU eviction",
                Metadata: new { evictedCount = count, freedBytes = bytes, triggeredBy = "DeepClean" }));
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "DeepCleanService: audit write failed for LRU eviction");
        }
    }

    /// <summary>
    /// Calculates total index directory size as:
    ///   Σ (all .db SQLite files in the app's working directory)
    ///   + Σ (all synced local files tracked by SharePoint/Confluence sync)
    ///
    /// This gives the full picture: database overhead + stored content blobs.
    /// </summary>
    private long CalculateTotalIndexSize()
    {
        var dbSize = CalculateDirectorySize(
            Path.Combine(Directory.GetCurrentDirectory(), "Db"),
            ["*.db", "*.db-wal", "*.db-shm"],
            SearchOption.TopDirectoryOnly);

        var knowledgeSize = CalculateDirectorySize(
            GetKnowledgeDirectory(),
            ["*"],
            SearchOption.AllDirectories);

        return dbSize + knowledgeSize;
    }

    private static long CalculateDirectorySize(string directory, string[] patterns, SearchOption searchOption)
    {
        if (!Directory.Exists(directory))
            return 0;

        long total = 0;
        foreach (var pattern in patterns)
        {
            foreach (var file in Directory.EnumerateFiles(directory, pattern, searchOption))
            {
                try { total += new FileInfo(file).Length; }
                catch (IOException) { /* file may be in use */ }
            }
        }

        return total;
    }

    /// <summary>
    /// Returns directories that may contain temporary plaintext fragments
    /// produced during indexing (text extraction, chunking, embedding prep).
    /// </summary>
    private string[] GetTempFragmentDirectories()
    {
        var knowledgeDir = GetKnowledgeDirectory();
        return
        [
            Path.Combine(knowledgeDir, ".tmp"),
            Path.Combine(knowledgeDir, ".chunks"),
            Path.Combine(AppContext.BaseDirectory, "tmp"),
            Path.Combine(Directory.GetCurrentDirectory(), "tmp"),
        ];
    }

    /// <summary>
    /// Determines if a file is a temporary indexing fragment by extension.
    /// </summary>
    private static bool IsTempFragment(string filePath)
    {
        var ext = Path.GetExtension(filePath);
        return ext is ".tmp" or ".chunk" or ".frag" or ".partial";
    }

    /// <summary>
    /// Resolves the knowledge folder path by querying the <see cref="KnowledgeStore"/>.
    /// Falls back to convention-based resolution.
    /// </summary>
    private string GetKnowledgeDirectory()
    {
        // KnowledgeStore manages the InternalKnowledge folder — derive the path
        // from the same root resolution the Core DI uses.
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            var srcDir = Path.Combine(current.FullName, "src");
            if (Directory.Exists(srcDir))
                return Path.Combine(current.FullName, "InternalKnowledge");

            current = current.Parent;
        }

        return Path.Combine(AppContext.BaseDirectory, "InternalKnowledge");
    }

    /// <summary>
    /// Mutable telemetry counters for a single maintenance cycle.
    /// Not logged with sensitive file names — only aggregate counts.
    /// </summary>
    private sealed class CycleSummary
    {
        public int OrphansRemoved { get; set; }
        public int FragmentsScrubbed { get; set; }
        public int LruEvicted { get; set; }
        public long LruEvictedBytes { get; set; }
    }
}
