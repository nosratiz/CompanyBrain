using Microsoft.EntityFrameworkCore;

namespace CompanyBrain.Dashboard.Data.Audit;

public sealed class AuditDbContext(DbContextOptions<AuditDbContext> options) : DbContext(options)
{
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<AuditLog>(entity =>
        {
            entity.ToTable("AuditLogs");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.EventType)
                .IsRequired()
                .HasConversion<string>()
                .HasMaxLength(40);

            entity.Property(e => e.ActorId).HasMaxLength(200);
            entity.Property(e => e.ActorEmail).HasMaxLength(300);
            entity.Property(e => e.TenantId).HasMaxLength(200);
            entity.Property(e => e.ResourceType).HasMaxLength(100);
            entity.Property(e => e.ResourceId).HasMaxLength(500);
            entity.Property(e => e.ResourceName).HasMaxLength(500);
            entity.Property(e => e.ErrorMessage).HasMaxLength(2000);
            entity.Property(e => e.IpAddress).HasMaxLength(50);

            entity.Property(e => e.Success).IsRequired().HasDefaultValue(true);
            entity.Property(e => e.Timestamp).IsRequired();

            entity.HasIndex(e => e.Timestamp);
            entity.HasIndex(e => e.EventType);
            entity.HasIndex(e => e.TenantId);
            entity.HasIndex(e => e.ActorEmail);
        });
    }
}
