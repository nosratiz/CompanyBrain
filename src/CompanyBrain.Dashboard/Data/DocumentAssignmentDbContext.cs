using CompanyBrain.Dashboard.Data.Models;
using Microsoft.EntityFrameworkCore;

namespace CompanyBrain.Dashboard.Data;

/// <summary>
/// SQLite database context for storing document-tenant assignments and custom MCP tools.
/// </summary>
public sealed class DocumentAssignmentDbContext : DbContext
{
    public DocumentAssignmentDbContext(DbContextOptions<DocumentAssignmentDbContext> options)
        : base(options)
    {
    }

    public DbSet<DocumentTenantAssignment> DocumentTenantAssignments => Set<DocumentTenantAssignment>();
    
    public DbSet<CustomTool> CustomTools => Set<CustomTool>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        ConfigureDocumentTenantAssignment(modelBuilder);
        ConfigureCustomTool(modelBuilder);
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
}
