using CompanyBrain.Dashboard.Data.Models;
using Microsoft.EntityFrameworkCore;

namespace CompanyBrain.Dashboard.Data;

/// <summary>
/// SQLite database context for storing document-tenant assignments.
/// </summary>
public sealed class DocumentAssignmentDbContext : DbContext
{
    public DocumentAssignmentDbContext(DbContextOptions<DocumentAssignmentDbContext> options)
        : base(options)
    {
    }

    public DbSet<DocumentTenantAssignment> DocumentTenantAssignments => Set<DocumentTenantAssignment>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

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
}
