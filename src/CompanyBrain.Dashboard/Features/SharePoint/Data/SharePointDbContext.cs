using Microsoft.EntityFrameworkCore;

namespace CompanyBrain.Dashboard.Features.SharePoint.Data;

/// <summary>
/// SQLite database context for SharePoint sync state, delta tokens, and file metadata.
/// </summary>
public sealed class SharePointDbContext(DbContextOptions<SharePointDbContext> options) : DbContext(options)
{
    public DbSet<SharePointAuthToken> AuthTokens => Set<SharePointAuthToken>();
    public DbSet<SyncedSharePointFolder> SyncedFolders => Set<SyncedSharePointFolder>();
    public DbSet<SyncedSharePointFile> SyncedFiles => Set<SyncedSharePointFile>();
    public DbSet<SharePointSyncConflict> SyncConflicts => Set<SharePointSyncConflict>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        ConfigureAuthToken(modelBuilder);
        ConfigureSyncedFolder(modelBuilder);
        ConfigureSyncedFile(modelBuilder);
        ConfigureSyncConflict(modelBuilder);
        ConfigureFts5Index(modelBuilder);
    }

    private static void ConfigureAuthToken(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<SharePointAuthToken>(entity =>
        {
            entity.ToTable("SharePointAuthTokens");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.TenantId)
                .IsRequired()
                .HasMaxLength(100);

            entity.Property(e => e.UserPrincipalName)
                .IsRequired()
                .HasMaxLength(256);

            entity.Property(e => e.EncryptedRefreshToken)
                .IsRequired();

            entity.Property(e => e.EncryptionNonce)
                .IsRequired();

            entity.Property(e => e.AuthTag)
                .IsRequired();

            entity.HasIndex(e => new { e.TenantId, e.IsActive });
        });
    }

    private static void ConfigureSyncedFolder(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<SyncedSharePointFolder>(entity =>
        {
            entity.ToTable("SharePointSyncedFolders");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.TenantId)
                .IsRequired()
                .HasMaxLength(100);

            entity.Property(e => e.SiteId)
                .IsRequired()
                .HasMaxLength(200);

            entity.Property(e => e.SiteName)
                .IsRequired()
                .HasMaxLength(500);

            entity.Property(e => e.DriveId)
                .IsRequired()
                .HasMaxLength(200);

            entity.Property(e => e.DriveName)
                .IsRequired()
                .HasMaxLength(500);

            entity.Property(e => e.FolderPath)
                .HasMaxLength(2000);

            entity.Property(e => e.LocalPath)
                .IsRequired()
                .HasMaxLength(2000);

            entity.Property(e => e.DeltaLink)
                .HasMaxLength(4000);

            entity.Property(e => e.LastSyncError)
                .HasMaxLength(2000);

            entity.HasIndex(e => e.TenantId);
            entity.HasIndex(e => new { e.SiteId, e.DriveId, e.FolderPath });
        });
    }

    private static void ConfigureSyncedFile(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<SyncedSharePointFile>(entity =>
        {
            entity.ToTable("SharePointSyncedFiles");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.DriveItemId)
                .IsRequired()
                .HasMaxLength(200);

            entity.Property(e => e.FileName)
                .IsRequired()
                .HasMaxLength(500);

            entity.Property(e => e.LocalPath)
                .IsRequired()
                .HasMaxLength(2000);

            entity.Property(e => e.RemotePath)
                .IsRequired()
                .HasMaxLength(2000);

            entity.Property(e => e.MimeType)
                .HasMaxLength(200);

            entity.Property(e => e.ETag)
                .HasMaxLength(500);

            // Relationship to folder
            entity.HasOne(e => e.SyncedFolder)
                .WithMany()
                .HasForeignKey(e => e.SyncedFolderId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(e => e.DriveItemId);
            entity.HasIndex(e => e.SyncedFolderId);
            entity.HasIndex(e => e.LocalPath).IsUnique();
        });
    }

    private static void ConfigureSyncConflict(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<SharePointSyncConflict>(entity =>
        {
            entity.ToTable("SharePointSyncConflicts");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.LocalPath)
                .IsRequired()
                .HasMaxLength(2000);

            entity.Property(e => e.RemotePath)
                .IsRequired()
                .HasMaxLength(2000);

            entity.Property(e => e.DriveId)
                .IsRequired()
                .HasMaxLength(200);

            entity.Property(e => e.ItemId)
                .IsRequired()
                .HasMaxLength(200);

            entity.Property(e => e.Status)
                .IsRequired()
                .HasMaxLength(50);

            entity.HasOne(e => e.SyncedFile)
                .WithMany()
                .HasForeignKey(e => e.SyncedFileId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(e => e.Status);
        });
    }

    private static void ConfigureFts5Index(ModelBuilder modelBuilder)
    {
        // Create FTS5 virtual table for full-text search
        // This is executed via raw SQL in migrations
        modelBuilder.Entity<SyncedSharePointFile>()
            .HasAnnotation("FTS5:Table", "SharePointFilesFts");
    }

    /// <summary>
    /// Creates the FTS5 virtual table for full-text search.
    /// Call this during database initialization.
    /// </summary>
    public async Task EnsureFts5TableAsync(CancellationToken cancellationToken = default)
    {
        const string createFts5Sql = """
            CREATE VIRTUAL TABLE IF NOT EXISTS SharePointFilesFts USING fts5(
                FileName,
                RemotePath,
                ExtractedContent,
                content='SharePointSyncedFiles',
                content_rowid='Id'
            );
            """;

        const string createTriggerInsert = """
            CREATE TRIGGER IF NOT EXISTS SharePointFiles_fts_insert AFTER INSERT ON SharePointSyncedFiles BEGIN
                INSERT INTO SharePointFilesFts(rowid, FileName, RemotePath, ExtractedContent)
                VALUES (new.Id, new.FileName, new.RemotePath, new.ExtractedContent);
            END;
            """;

        const string createTriggerDelete = """
            CREATE TRIGGER IF NOT EXISTS SharePointFiles_fts_delete AFTER DELETE ON SharePointSyncedFiles BEGIN
                INSERT INTO SharePointFilesFts(SharePointFilesFts, rowid, FileName, RemotePath, ExtractedContent)
                VALUES ('delete', old.Id, old.FileName, old.RemotePath, old.ExtractedContent);
            END;
            """;

        const string createTriggerUpdate = """
            CREATE TRIGGER IF NOT EXISTS SharePointFiles_fts_update AFTER UPDATE ON SharePointSyncedFiles BEGIN
                INSERT INTO SharePointFilesFts(SharePointFilesFts, rowid, FileName, RemotePath, ExtractedContent)
                VALUES ('delete', old.Id, old.FileName, old.RemotePath, old.ExtractedContent);
                INSERT INTO SharePointFilesFts(rowid, FileName, RemotePath, ExtractedContent)
                VALUES (new.Id, new.FileName, new.RemotePath, new.ExtractedContent);
            END;
            """;

        await Database.ExecuteSqlRawAsync(createFts5Sql, cancellationToken);
        await Database.ExecuteSqlRawAsync(createTriggerInsert, cancellationToken);
        await Database.ExecuteSqlRawAsync(createTriggerDelete, cancellationToken);
        await Database.ExecuteSqlRawAsync(createTriggerUpdate, cancellationToken);
    }

    /// <summary>
    /// Searches the SharePoint files using FTS5 full-text search.
    /// </summary>
    public async Task<List<SyncedSharePointFile>> SearchFilesAsync(
        string query,
        int maxResults = 10,
        CancellationToken cancellationToken = default)
    {
        // Escape single quotes and prepare FTS5 query
        var sanitizedQuery = query.Replace("'", "''");

        var sql = $"""
            SELECT f.* FROM SharePointSyncedFiles f
            INNER JOIN SharePointFilesFts fts ON f.Id = fts.rowid
            WHERE SharePointFilesFts MATCH '{sanitizedQuery}'
            ORDER BY bm25(SharePointFilesFts)
            LIMIT {maxResults}
            """;

        return await SyncedFiles
            .FromSqlRaw(sql)
            .Include(f => f.SyncedFolder)
            .ToListAsync(cancellationToken);
    }
}
