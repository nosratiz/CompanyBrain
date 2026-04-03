using CompanyBrain.MultiTenant.Domain;
using Microsoft.EntityFrameworkCore;

namespace CompanyBrain.MultiTenant.Data;

public sealed class TenantDbContext(DbContextOptions<TenantDbContext> options) : DbContext(options)
{
    public DbSet<Tenant> Tenants => Set<Tenant>();
    public DbSet<ApiKey> ApiKeys => Set<ApiKey>();
    public DbSet<TenantUser> Users => Set<TenantUser>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Tenant>(entity =>
        {
            entity.HasKey(t => t.Id);
            entity.Property(t => t.Name).HasMaxLength(200).IsRequired();
            entity.Property(t => t.Slug).HasMaxLength(100).IsRequired();
            entity.HasIndex(t => t.Slug).IsUnique();
            entity.Property(t => t.Status).HasConversion<string>();
            entity.Property(t => t.Plan).HasConversion<string>();
        });

        modelBuilder.Entity<ApiKey>(entity =>
        {
            entity.HasKey(k => k.Id);
            entity.Property(k => k.Name).HasMaxLength(200).IsRequired();
            entity.Property(k => k.KeyHash).HasMaxLength(100).IsRequired();
            entity.Property(k => k.KeyPrefix).HasMaxLength(20).IsRequired();
            entity.HasIndex(k => k.KeyHash).IsUnique();
            entity.HasIndex(k => k.KeyPrefix);
            entity.Property(k => k.Scope).HasConversion<string>();

            entity.HasOne(k => k.Tenant)
                .WithMany(t => t.ApiKeys)
                .HasForeignKey(k => k.TenantId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<TenantUser>(entity =>
        {
            entity.HasKey(u => u.Id);
            entity.Property(u => u.Email).HasMaxLength(300).IsRequired();
            entity.Property(u => u.DisplayName).HasMaxLength(200).IsRequired();
            entity.HasIndex(u => new { u.TenantId, u.Email }).IsUnique();
            entity.Property(u => u.Role).HasConversion<string>();

            entity.HasOne(u => u.Tenant)
                .WithMany(t => t.Users)
                .HasForeignKey(u => u.TenantId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        base.OnModelCreating(modelBuilder);
    }
}
