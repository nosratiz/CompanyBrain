using CompanyBrain.Dashboard.Data;
using CompanyBrain.Dashboard.Data.Audit;
using CompanyBrain.Dashboard.Features.License;
using CompanyBrain.Dashboard.Mcp.Collections;
using CompanyBrain.Dashboard.Middleware;
using Microsoft.EntityFrameworkCore;

namespace CompanyBrain.Dashboard.DependencyInjection;

/// <summary>
/// Extension methods for configuring the Dashboard application pipeline.
/// </summary>
public static class DashboardApplicationBuilderExtensions
{
    /// <summary>
    /// Configures the Dashboard middleware pipeline.
    /// </summary>
    public static WebApplication UseDashboardMiddleware(this WebApplication app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.UseGlobalExceptionHandler();
        app.UseRequestLogging();
        app.UseSecurityHeaders();

        if (app.Environment.IsDevelopment())
        {
            app.UseDeveloperExceptionPage();
        }
        else
        {
            app.UseHsts();
        }

        app.UseCors();
        app.UseSwagger();
        app.UseSwaggerUI();

        app.UseStaticFiles();
        app.UseAuthentication();
        app.UseAuthorization();
        app.UseMiddleware<LicenseGuardMiddleware>();
        app.UseMiddleware<McpCollectionLicenseMiddleware>();
        app.UseAntiforgery();

        return app;
    }

    /// <summary>
    /// Ensures the SQLite database is created on startup.
    /// </summary>
    public static async Task<WebApplication> InitializeDatabaseAsync(this WebApplication app)
    {
        ArgumentNullException.ThrowIfNull(app);

        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DocumentAssignmentDbContext>();
        await db.Database.EnsureCreatedAsync();

        // EnsureCreated does NOT add tables that were introduced after the database was first created.
        // Patch any tables/indexes that the current model expects but a pre-existing DB lacks.
        await EnsureCollectionPoliciesTableAsync(db);
        await EnsureDeepRootEmbeddingSettingsTableAsync(db);
        await EnsureSyncSchedulesTableAsync(db);
        await EnsureChatBotSettingsTableAsync(db);
        await EnsureConversationThreadsTableAsync(db);
        await EnsureNotionColumnsAsync(db);

        return app;
    }

    /// <summary>
    /// Ensures the audit SQLite database is created on startup.
    /// </summary>
    public static async Task<WebApplication> InitializeAuditDatabaseAsync(this WebApplication app)
    {
        ArgumentNullException.ThrowIfNull(app);

        var factory = app.Services.GetRequiredService<IDbContextFactory<AuditDbContext>>();
        await using var db = await factory.CreateDbContextAsync();
        await db.Database.EnsureCreatedAsync();

        return app;
    }

    /// <summary>
    /// Initializes the main and audit databases for the stdio MCP host.
    /// </summary>
    public static async Task InitializeMcpDatabasesAsync(this IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DocumentAssignmentDbContext>();
        await db.Database.EnsureCreatedAsync();
        await EnsureCollectionPoliciesTableAsync(db);
        await EnsureDeepRootEmbeddingSettingsTableAsync(db);
        await EnsureSyncSchedulesTableAsync(db);
        await EnsureChatBotSettingsTableAsync(db);
        await EnsureConversationThreadsTableAsync(db);
        await EnsureNotionColumnsAsync(db);

        var auditFactory = services.GetRequiredService<IDbContextFactory<AuditDbContext>>();
        await using var auditDb = await auditFactory.CreateDbContextAsync();
        await auditDb.Database.EnsureCreatedAsync();
    }

    private static async Task EnsureCollectionPoliciesTableAsync(DocumentAssignmentDbContext db)
    {
        const string createTableSql = """
            CREATE TABLE IF NOT EXISTS "CollectionPolicies" (
                "Id" INTEGER NOT NULL CONSTRAINT "PK_CollectionPolicies" PRIMARY KEY AUTOINCREMENT,
                "CollectionId" TEXT NOT NULL,
                "Department" TEXT NOT NULL,
                "PrivacyAggressionPercent" INTEGER NOT NULL DEFAULT 50,
                "IsSyncing" INTEGER NOT NULL DEFAULT 0,
                "UpdatedAtUtc" TEXT NOT NULL
            );
            """;
        const string createIndexSql = """
            CREATE UNIQUE INDEX IF NOT EXISTS "IX_CollectionPolicies_CollectionId_Department"
                ON "CollectionPolicies" ("CollectionId", "Department");
            """;

        await db.Database.ExecuteSqlRawAsync(createTableSql);
        await db.Database.ExecuteSqlRawAsync(createIndexSql);
    }

    private static async Task EnsureDeepRootEmbeddingSettingsTableAsync(DocumentAssignmentDbContext db)
    {
        const string createTableSql = """
            CREATE TABLE IF NOT EXISTS "DeepRootEmbeddingSettings" (
                "Id" TEXT NOT NULL CONSTRAINT "PK_DeepRootEmbeddingSettings" PRIMARY KEY,
                "Provider" TEXT NOT NULL DEFAULT 'None',
                "Model" TEXT NOT NULL DEFAULT '',
                "Dimensions" INTEGER NOT NULL DEFAULT 0,
                "EncryptedApiKey" TEXT NOT NULL DEFAULT '',
                "Endpoint" TEXT NOT NULL DEFAULT '',
                "DatabasePath" TEXT NOT NULL DEFAULT '',
                "UpdatedAtUtc" TEXT NOT NULL
            );
            """;
        await db.Database.ExecuteSqlRawAsync(createTableSql);
    }

    private static async Task EnsureSyncSchedulesTableAsync(DocumentAssignmentDbContext db)
    {
        const string createTableSql = """
            CREATE TABLE IF NOT EXISTS "SyncSchedules" (
                "Id"                     INTEGER NOT NULL CONSTRAINT "PK_SyncSchedules" PRIMARY KEY AUTOINCREMENT,
                "SourceUrl"              TEXT    NOT NULL,
                "SourceType"             TEXT    NOT NULL DEFAULT 'WebWiki',
                "CollectionName"         TEXT,
                "CronExpression"         TEXT    NOT NULL,
                "LastSyncUtc"            TEXT,
                "LastContentHash"        TEXT,
                "IsActive"               INTEGER NOT NULL DEFAULT 1,
                "LastErrorMessage"       TEXT,
                "ConsecutiveFailureCount" INTEGER NOT NULL DEFAULT 0,
                "NextRetryUtc"           TEXT,
                "CreatedAtUtc"           TEXT    NOT NULL
            );
            """;
        const string createIndexActiveSql = """
            CREATE INDEX IF NOT EXISTS "IX_SyncSchedules_IsActive"
                ON "SyncSchedules" ("IsActive");
            """;
        const string createIndexUrlSql = """
            CREATE INDEX IF NOT EXISTS "IX_SyncSchedules_SourceUrl"
                ON "SyncSchedules" ("SourceUrl");
            """;

        await db.Database.ExecuteSqlRawAsync(createTableSql);
        await db.Database.ExecuteSqlRawAsync(createIndexActiveSql);
        await db.Database.ExecuteSqlRawAsync(createIndexUrlSql);
    }

    private static async Task EnsureChatBotSettingsTableAsync(DocumentAssignmentDbContext db)
    {
        const string createTableSql = """
            CREATE TABLE IF NOT EXISTS "ChatBotSettings" (
                "Id"                          TEXT    NOT NULL CONSTRAINT "PK_ChatBotSettings" PRIMARY KEY,
                "SlackEnabled"                INTEGER NOT NULL DEFAULT 0,
                "EncryptedSlackBotToken"      TEXT    NOT NULL DEFAULT '',
                "EncryptedSlackSigningSecret" TEXT    NOT NULL DEFAULT '',
                "TeamsEnabled"                INTEGER NOT NULL DEFAULT 0,
                "TeamsAppId"                  TEXT    NOT NULL DEFAULT '',
                "EncryptedTeamsAppPassword"   TEXT    NOT NULL DEFAULT '',
                "TunnelEnabled"               INTEGER NOT NULL DEFAULT 0,
                "TunnelUrl"                   TEXT    NOT NULL DEFAULT '',
                "UpdatedAtUtc"                TEXT    NOT NULL
            );
            """;
        await db.Database.ExecuteSqlRawAsync(createTableSql);

        // Add DevTunnelId column to existing installations (safe: ignored if already present).
        try
        {
            await db.Database.ExecuteSqlRawAsync(
                "ALTER TABLE ChatBotSettings ADD COLUMN \"DevTunnelId\" TEXT NOT NULL DEFAULT ''");
        }
        catch { /* column already exists — safe to ignore */ }
    }

    private static async Task EnsureConversationThreadsTableAsync(DocumentAssignmentDbContext db)
    {
        const string createTableSql = """
            CREATE TABLE IF NOT EXISTS "ConversationThreads" (
                "Id"                INTEGER NOT NULL CONSTRAINT "PK_ConversationThreads" PRIMARY KEY AUTOINCREMENT,
                "Platform"          TEXT    NOT NULL,
                "ExternalThreadId"  TEXT    NOT NULL,
                "ExternalChannelId" TEXT    NOT NULL,
                "ServiceUrl"        TEXT    NOT NULL DEFAULT '',
                "SessionId"         TEXT    NOT NULL,
                "IsActive"          INTEGER NOT NULL DEFAULT 1,
                "CreatedAtUtc"      TEXT    NOT NULL,
                "LastActivityUtc"   TEXT    NOT NULL
            );
            """;
        const string createUniqueIndexSql = """
            CREATE UNIQUE INDEX IF NOT EXISTS "IX_ConversationThreads_Platform_ExternalThreadId"
                ON "ConversationThreads" ("Platform", "ExternalThreadId");
            """;
        const string createActiveIndexSql = """
            CREATE INDEX IF NOT EXISTS "IX_ConversationThreads_IsActive"
                ON "ConversationThreads" ("IsActive");
            """;

        await db.Database.ExecuteSqlRawAsync(createTableSql);
        await db.Database.ExecuteSqlRawAsync(createUniqueIndexSql);
        await db.Database.ExecuteSqlRawAsync(createActiveIndexSql);
    }

    private static async Task EnsureNotionColumnsAsync(DocumentAssignmentDbContext db)
    {
        // SQLite throws when you add a column that already exists; wrap each in try/catch.
        try
        {
            await db.Database.ExecuteSqlRawAsync(
                "ALTER TABLE AppSettings ADD COLUMN \"NotionApiToken\" TEXT NOT NULL DEFAULT ''");
        }
        catch (Microsoft.Data.Sqlite.SqliteException)
        {
            // Column already exists — expected on databases that were created with the new schema.
        }

        try
        {
            await db.Database.ExecuteSqlRawAsync(
                "ALTER TABLE AppSettings ADD COLUMN \"NotionWorkspaceFilter\" TEXT NOT NULL DEFAULT ''");
        }
        catch (Microsoft.Data.Sqlite.SqliteException)
        {
            // Column already exists.
        }
    }
}
