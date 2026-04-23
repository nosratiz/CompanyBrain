using CompanyBrain.Dashboard.Data.Models;
using CompanyBrain.Dashboard.Features.AutoSync.Models;
using CompanyBrain.Dashboard.Features.ChatRelay.Models;
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

    public DbSet<CollectionPolicy> CollectionPolicies => Set<CollectionPolicy>();
    
    public DbSet<CustomTool> CustomTools => Set<CustomTool>();
    
    public DbSet<AppSettings> AppSettings => Set<AppSettings>();

    public DbSet<DeepRootEmbeddingSettings> DeepRootEmbeddingSettings => Set<DeepRootEmbeddingSettings>();

    public DbSet<SyncSchedule> SyncSchedules => Set<SyncSchedule>();

    public DbSet<ChatBotSettings> ChatBotSettings => Set<ChatBotSettings>();

    public DbSet<ConversationThread> ConversationThreads => Set<ConversationThread>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        ConfigureDocumentTenantAssignment(modelBuilder);
        ConfigureCollectionPolicies(modelBuilder);
        ConfigureCustomTool(modelBuilder);
        ConfigureAppSettings(modelBuilder);
        ConfigureDeepRootEmbeddingSettings(modelBuilder);
        ConfigureSyncSchedules(modelBuilder);
        ConfigureChatBotSettings(modelBuilder);
        ConfigureConversationThreads(modelBuilder);
    }
    
    private static void ConfigureDocumentTenantAssignment(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<DocumentTenantAssignment>(entity =>
        {
            entity.ToTable("DocumentTenantAssignments");
            
            entity.HasKey(e => e.Id);

            entity.Property(e => e.CollectionId)
                .IsRequired()
                .HasMaxLength(120);
            
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

            // Create a unique index on Collection + FileName + TenantId to prevent duplicate assignments
            entity.HasIndex(e => new { e.CollectionId, e.FileName, e.TenantId })
                .IsUnique();
            
            // Index for querying by tenant
            entity.HasIndex(e => e.TenantId);
            
            // Index for querying by file
            entity.HasIndex(e => e.FileName);

            // Index for strict collection scoping during query-time enforcement
            entity.HasIndex(e => new { e.CollectionId, e.TenantId });
        });
    }

    private static void ConfigureCollectionPolicies(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<CollectionPolicy>(entity =>
        {
            entity.ToTable("CollectionPolicies");

            entity.HasKey(e => e.Id);

            entity.Property(e => e.CollectionId)
                .IsRequired()
                .HasMaxLength(120);

            entity.Property(e => e.Department)
                .IsRequired()
                .HasMaxLength(120);

            entity.Property(e => e.PrivacyAggressionPercent)
                .IsRequired()
                .HasDefaultValue(50);

            entity.Property(e => e.IsSyncing)
                .IsRequired()
                .HasDefaultValue(false);

            entity.Property(e => e.UpdatedAtUtc)
                .IsRequired();

            entity.HasIndex(e => new { e.CollectionId, e.Department })
                .IsUnique();
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

            // Notion sync settings
            entity.Property(e => e.NotionApiToken)
                .HasMaxLength(4096)
                .HasDefaultValue(string.Empty);

            entity.Property(e => e.NotionWorkspaceFilter)
                .HasMaxLength(2000)
                .HasDefaultValue(string.Empty);

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
                SharePointSyncEnabled = false,
                NotionApiToken = string.Empty,
                NotionWorkspaceFilter = string.Empty
            });
        });
    }

    private static void ConfigureDeepRootEmbeddingSettings(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<DeepRootEmbeddingSettings>(entity =>
        {
            entity.ToTable("DeepRootEmbeddingSettings");

            entity.HasKey(e => e.Id);

            entity.Property(e => e.Provider)
                .IsRequired()
                .HasMaxLength(40)
                .HasDefaultValue("None");

            entity.Property(e => e.Model)
                .HasMaxLength(120)
                .HasDefaultValue(string.Empty);

            entity.Property(e => e.Dimensions)
                .IsRequired()
                .HasDefaultValue(0);

            entity.Property(e => e.EncryptedApiKey)
                .HasMaxLength(4096)
                .HasDefaultValue(string.Empty);

            entity.Property(e => e.Endpoint)
                .HasMaxLength(500)
                .HasDefaultValue(string.Empty);

            entity.Property(e => e.DatabasePath)
                .HasMaxLength(500)
                .HasDefaultValue(string.Empty);

            entity.Property(e => e.UpdatedAtUtc)
                .IsRequired();

            entity.HasData(new DeepRootEmbeddingSettings
            {
                Id = DeepRootEmbeddingSettingsConstants.SingletonId,
                Provider = "None",
                Model = string.Empty,
                Dimensions = 0,
                EncryptedApiKey = string.Empty,
                Endpoint = string.Empty,
                DatabasePath = string.Empty,
                UpdatedAtUtc = DateTime.UtcNow,
            });
        });
    }

    private static void ConfigureSyncSchedules(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<SyncSchedule>(entity =>
        {
            entity.ToTable("SyncSchedules");

            entity.HasKey(e => e.Id);

            entity.Property(e => e.SourceUrl)
                .IsRequired()
                .HasMaxLength(2000);

            entity.Property(e => e.SourceType)
                .IsRequired()
                .HasConversion<string>()
                .HasMaxLength(40);

            entity.Property(e => e.CollectionName)
                .HasMaxLength(200);

            entity.Property(e => e.CronExpression)
                .IsRequired()
                .HasMaxLength(100);

            entity.Property(e => e.LastContentHash)
                .HasMaxLength(64);

            entity.Property(e => e.LastErrorMessage)
                .HasMaxLength(2000);

            entity.Property(e => e.IsActive)
                .IsRequired()
                .HasDefaultValue(true);

            entity.Property(e => e.ConsecutiveFailureCount)
                .IsRequired()
                .HasDefaultValue(0);

            entity.Property(e => e.CreatedAtUtc)
                .IsRequired();

            // Index to efficiently fetch active schedules for the worker loop
            entity.HasIndex(e => e.IsActive);

            // Index to look up by URL (uniqueness is NOT enforced — same URL may appear
            // in multiple collections or with different cron expressions)
            entity.HasIndex(e => e.SourceUrl);
        });
    }

    private static void ConfigureChatBotSettings(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ChatBotSettings>(entity =>
        {
            entity.ToTable("ChatBotSettings");

            entity.HasKey(e => e.Id);

            entity.Property(e => e.EncryptedSlackBotToken).HasMaxLength(4096).HasDefaultValue(string.Empty);
            entity.Property(e => e.EncryptedSlackSigningSecret).HasMaxLength(4096).HasDefaultValue(string.Empty);
            entity.Property(e => e.TeamsAppId).HasMaxLength(100).HasDefaultValue(string.Empty);
            entity.Property(e => e.EncryptedTeamsAppPassword).HasMaxLength(4096).HasDefaultValue(string.Empty);
            entity.Property(e => e.DevTunnelId).HasMaxLength(200).HasDefaultValue(string.Empty);
            entity.Property(e => e.TunnelUrl).HasMaxLength(500).HasDefaultValue(string.Empty);
            entity.Property(e => e.UpdatedAtUtc).IsRequired();
        });
    }

    private static void ConfigureConversationThreads(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ConversationThread>(entity =>
        {
            entity.ToTable("ConversationThreads");

            entity.HasKey(e => e.Id);

            entity.Property(e => e.Platform)
                .IsRequired()
                .HasConversion<string>()
                .HasMaxLength(20);

            entity.Property(e => e.ExternalThreadId).IsRequired().HasMaxLength(500);
            entity.Property(e => e.ExternalChannelId).IsRequired().HasMaxLength(500);
            entity.Property(e => e.ServiceUrl).IsRequired().HasMaxLength(500).HasDefaultValue(string.Empty);
            entity.Property(e => e.SessionId).IsRequired().HasMaxLength(64);
            entity.Property(e => e.IsActive).IsRequired().HasDefaultValue(true);
            entity.Property(e => e.CreatedAtUtc).IsRequired();
            entity.Property(e => e.LastActivityUtc).IsRequired();

            entity.HasIndex(e => new { e.Platform, e.ExternalThreadId }).IsUnique();
            entity.HasIndex(e => e.IsActive);
        });
    }
}
