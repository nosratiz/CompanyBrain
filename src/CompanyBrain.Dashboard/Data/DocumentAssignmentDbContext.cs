using CompanyBrain.Dashboard.Data.Models;
using Microsoft.EntityFrameworkCore;

namespace CompanyBrain.Dashboard.Data;

/// <summary>
/// SQLite database context for storing document-tenant assignments, custom MCP tools, and application settings.
/// </summary>
public sealed class DocumentAssignmentDbContext : DbContext
{
    public DocumentAssignmentDbContext(DbContextOptions<DocumentAssignmentDbContext> options)
        : base(options)
    {
    }

    public DbSet<DocumentTenantAssignment> DocumentTenantAssignments => Set<DocumentTenantAssignment>();
    
    public DbSet<CustomTool> CustomTools => Set<CustomTool>();
    
    public DbSet<AppSettings> AppSettings => Set<AppSettings>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        ConfigureDocumentTenantAssignment(modelBuilder);
        ConfigureCustomTool(modelBuilder);
        ConfigureAppSettings(modelBuilder);
    }
    
    private static void ConfigureDocumentTenantAssignment(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<DocumentTenantAssignment>(entity =>
        {
            entity.ToTable("DocumentTenantAssignments");
            
            entity.HasKey(e => e.Id);
            
            entity.Property(e => e.FileName)
                .IsRequired()
                .HasMaxLength(500);
            
            entity.Property(e => e.TenantId)
                .IsRequired();
            
            entity.Property(e => e.TenantName)
                .IsRequired()
                .HasMaxLength(200);
            
            entity.Property(e => e.CreatedAtUtc)
                .IsRequired();

            // Create a unique index on FileName + TenantId to prevent duplicate assignments
            entity.HasIndex(e => new { e.FileName, e.TenantId })
                .IsUnique();
            
            // Index for querying by tenant
            entity.HasIndex(e => e.TenantId);
            
            // Index for querying by file
            entity.HasIndex(e => e.FileName);
        });
    }
    
    private static void ConfigureCustomTool(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<CustomTool>(entity =>
        {
            entity.ToTable("CustomTools");
            
            entity.HasKey(e => e.Id);
            
            entity.Property(e => e.TenantId)
                .IsRequired();
            
            entity.Property(e => e.Name)
                .IsRequired()
                .HasMaxLength(100);
            
            entity.Property(e => e.Description)
                .IsRequired()
                .HasMaxLength(1000);
            
            entity.Property(e => e.JsonSchema)
                .IsRequired();
            
            entity.Property(e => e.CSharpCode)
                .IsRequired();
            
            entity.Property(e => e.IsEnabled)
                .IsRequired()
                .HasDefaultValue(true);
            
            entity.Property(e => e.IsWriteEnabled)
                .IsRequired()
                .HasDefaultValue(false);
            
            entity.Property(e => e.CreatedAtUtc)
                .IsRequired();
            
            entity.Property(e => e.CreatedBy)
                .HasMaxLength(200);
            
            entity.Property(e => e.Version)
                .IsRequired()
                .HasDefaultValue(1)
                .IsConcurrencyToken();

            // Unique tool name per tenant
            entity.HasIndex(e => new { e.TenantId, e.Name })
                .IsUnique();
            
            // Index for querying enabled tools by tenant
            entity.HasIndex(e => new { e.TenantId, e.IsEnabled });
        });
    }
    
    private static void ConfigureAppSettings(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<AppSettings>(entity =>
        {
            entity.ToTable("AppSettings");
            
            entity.HasKey(e => e.Id);
            
            entity.Property(e => e.EnablePiiMasking)
                .IsRequired()
                .HasDefaultValue(false);
            
            entity.Property(e => e.MaxStorageGb)
                .IsRequired()
                .HasDefaultValue(10);
            
            entity.Property(e => e.SecurityMode)
                .IsRequired()
                .HasMaxLength(50)
                .HasDefaultValue("Moderate");
            
            entity.Property(e => e.ExcludedPatterns)
                .HasMaxLength(2000)
                .HasDefaultValue(string.Empty);
            
            entity.Property(e => e.SystemPromptPrefix)
                .HasMaxLength(4000)
                .HasDefaultValue(string.Empty);
            
            entity.Property(e => e.TenantId);
            
            entity.Property(e => e.UpdatedAtUtc)
                .IsRequired();
            
            entity.Property(e => e.McpRequireAuth)
                .IsRequired()
                .HasDefaultValue(false);
            
            entity.Property(e => e.McpIpWhitelist)
                .HasMaxLength(2000)
                .HasDefaultValue(string.Empty);
            
            entity.Property(e => e.McpEnableIpWhitelist)
                .IsRequired()
                .HasDefaultValue(false);
            
            entity.Property(e => e.McpApiKey)
                .HasMaxLength(256)
                .HasDefaultValue(string.Empty);
            
            // SharePoint sync settings
            entity.Property(e => e.SharePointClientId)
                .HasMaxLength(100)
                .HasDefaultValue(string.Empty);
            
            entity.Property(e => e.SharePointTenantId)
                .HasMaxLength(100)
                .HasDefaultValue(string.Empty);
            
            entity.Property(e => e.SharePointClientSecret)
                .HasMaxLength(500)
                .HasDefaultValue(string.Empty);
            
            entity.Property(e => e.SharePointSyncIntervalMinutes)
                .IsRequired()
                .HasDefaultValue(30);
            
            entity.Property(e => e.SharePointLocalBasePath)
                .HasMaxLength(500)
                .HasDefaultValue(string.Empty);
            
            entity.Property(e => e.SharePointSyncEnabled)
                .IsRequired()
                .HasDefaultValue(false);
            
            // Seed the singleton settings row
            entity.HasData(new AppSettings
            {
                Id = AppSettingsConstants.SingletonId,
                EnablePiiMasking = false,
                MaxStorageGb = 10,
                SecurityMode = "Moderate",
                ExcludedPatterns = string.Empty,
                SystemPromptPrefix = string.Empty,
                TenantId = null,
                UpdatedAtUtc = DateTime.UtcNow,
                McpRequireAuth = false,
                McpIpWhitelist = string.Empty,
                McpEnableIpWhitelist = false,
                McpApiKey = string.Empty,
                SharePointClientId = string.Empty,
                SharePointTenantId = string.Empty,
                SharePointClientSecret = string.Empty,
                SharePointSyncIntervalMinutes = 30,
                SharePointLocalBasePath = string.Empty,
                SharePointSyncEnabled = false
            });
        });
    }
}
