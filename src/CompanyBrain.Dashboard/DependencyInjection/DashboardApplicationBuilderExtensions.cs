using CompanyBrain.Dashboard.Data;
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

        return app;
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
}
