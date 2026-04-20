using CompanyBrain.Dashboard.Data;
using CompanyBrain.Dashboard.Features.SharePoint.Data;
using CompanyBrain.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace CompanyBrain.Dashboard.Features.DeepClean;

/// <summary>
/// Data access for the DeepClean service. Handles orphan detection, LRU queries,
/// and SQLite maintenance (VACUUM/ANALYZE) across all application databases.
/// Uses <see cref="IDbContextFactory{TContext}"/> for safe concurrent access.
/// </summary>
internal sealed class DeepCleanRepository(
    IDbContextFactory<SharePointDbContext> sharePointDbFactory,
    IDbContextFactory<DocumentAssignmentDbContext> documentDbFactory,
    KnowledgeStore knowledgeStore,
    ILogger<DeepCleanRepository> logger)
{
    // ──────────────────────────── Orphan Detection ────────────────────────────

    /// <summary>
    /// Finds SharePoint synced file records whose local file no longer exists on disk.
    /// These are "orphaned embeddings" — index entries with no backing content.
    /// </summary>
    public async Task<int> RemoveOrphanedSharePointRecordsAsync(CancellationToken cancellationToken)
    {
        await using var db = await sharePointDbFactory.CreateDbContextAsync(cancellationToken);

        var allRecords = await db.SyncedFiles
            .Select(f => new { f.Id, f.LocalPath })
            .ToListAsync(cancellationToken);

        var orphanIds = allRecords
            .Where(r => !File.Exists(r.LocalPath))
            .Select(r => r.Id)
            .ToList();

        if (orphanIds.Count == 0)
            return 0;

        // Batch delete by IDs — avoids loading full entities
        var deleted = await db.SyncedFiles
            .Where(f => orphanIds.Contains(f.Id))
            .ExecuteDeleteAsync(cancellationToken);

        return deleted;
    }

    /// <summary>
    /// Finds document-tenant assignment records where the referenced knowledge file
    /// no longer exists in the knowledge store on disk.
    /// </summary>
    public async Task<int> RemoveOrphanedAssignmentsAsync(CancellationToken cancellationToken)
    {
        await using var db = await documentDbFactory.CreateDbContextAsync(cancellationToken);

        // Get all known resource filenames from disk
        var knownFiles = (await knowledgeStore.ListResourcesAsync(cancellationToken))
            .Select(r => Path.GetFileName(r.Name))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var allAssignments = await db.DocumentTenantAssignments
            .Select(a => new { a.Id, a.FileName })
            .ToListAsync(cancellationToken);

        var orphanIds = allAssignments
            .Where(a => !knownFiles.Contains(a.FileName))
            .Select(a => a.Id)
            .ToList();

        if (orphanIds.Count == 0)
            return 0;

        var deleted = await db.DocumentTenantAssignments
            .Where(a => orphanIds.Contains(a.Id))
            .ExecuteDeleteAsync(cancellationToken);

        return deleted;
    }

    // ──────────────────────────── LRU Eviction ────────────────────────────

    /// <summary>
    /// Returns the least-recently-synced SharePoint file records, ordered ascending
    /// by <c>LastSyncedAtUtc</c>. Used by the quota guardrail to decide what to evict.
    /// </summary>
    public async Task<List<LruCandidate>> GetLruCandidatesAsync(
        int limit,
        CancellationToken cancellationToken)
    {
        await using var db = await sharePointDbFactory.CreateDbContextAsync(cancellationToken);

        return await db.SyncedFiles
            .OrderBy(f => f.LastSyncedAtUtc)
            .Take(limit)
            .Select(f => new LruCandidate(f.Id, f.LocalPath, f.Size, f.LastSyncedAtUtc))
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// Deletes SharePoint synced file records by their IDs. Called after the local
    /// files have been securely erased.
    /// </summary>
    public async Task<int> DeleteSharePointRecordsByIdAsync(
        IReadOnlyCollection<int> ids,
        CancellationToken cancellationToken)
    {
        if (ids.Count == 0)
            return 0;

        await using var db = await sharePointDbFactory.CreateDbContextAsync(cancellationToken);

        return await db.SyncedFiles
            .Where(f => ids.Contains(f.Id))
            .ExecuteDeleteAsync(cancellationToken);
    }

    // ──────────────────────────── SQLite Maintenance ────────────────────────────

    /// <summary>
    /// Runs VACUUM and ANALYZE on all application SQLite databases.
    /// Uses raw ADO.NET to execute outside EF Core's transaction scope.
    /// WAL mode allows readers to proceed concurrently during these operations.
    /// </summary>
    public async Task RunMaintenanceAsync(CancellationToken cancellationToken)
    {
        await VacuumAndAnalyzeAsync(sharePointDbFactory, "SharePoint", cancellationToken);
        await VacuumAndAnalyzeAsync(documentDbFactory, "DocumentAssignment", cancellationToken);
    }

    private async Task VacuumAndAnalyzeAsync<TContext>(
        IDbContextFactory<TContext> factory,
        string dbName,
        CancellationToken cancellationToken)
        where TContext : DbContext
    {
        try
        {
            await using var db = await factory.CreateDbContextAsync(cancellationToken);
            var connection = db.Database.GetDbConnection();
            await connection.OpenAsync(cancellationToken);

            // Ensure WAL mode is active so readers aren't blocked
            await ExecuteNonQueryAsync(connection, "PRAGMA journal_mode=WAL;", cancellationToken);

            // ANALYZE updates query planner statistics
            await ExecuteNonQueryAsync(connection, "ANALYZE;", cancellationToken);

            // VACUUM rebuilds the database file, reclaiming free pages
            // Note: VACUUM requires exclusive access momentarily but WAL mode
            // keeps the window narrow; readers see the old snapshot until done.
            await ExecuteNonQueryAsync(connection, "VACUUM;", cancellationToken);

            logger.LogInformation("{DbName} database: ANALYZE + VACUUM completed", dbName);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "{DbName} database maintenance failed", dbName);
        }
    }

    private static async Task ExecuteNonQueryAsync(
        System.Data.Common.DbConnection connection,
        string sql,
        CancellationToken cancellationToken)
    {
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = sql;
        await cmd.ExecuteNonQueryAsync(cancellationToken);
    }
}

/// <summary>
/// Lightweight projection of a SharePoint synced file for LRU eviction decisions.
/// </summary>
internal sealed record LruCandidate(
    int Id,
    string LocalPath,
    long SizeBytes,
    DateTimeOffset LastAccessedUtc);
