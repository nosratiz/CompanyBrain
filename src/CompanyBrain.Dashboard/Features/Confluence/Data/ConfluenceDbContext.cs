using Microsoft.EntityFrameworkCore;

namespace CompanyBrain.Dashboard.Features.Confluence.Data;

public sealed class ConfluenceDbContext(DbContextOptions<ConfluenceDbContext> options) : DbContext(options)
{
    public DbSet<ConfluenceCredentials> Credentials { get; set; }
    public DbSet<ConfluenceSyncedSpace> SyncedSpaces { get; set; }
    public DbSet<ConfluenceSyncedPage> SyncedPages { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ConfluenceCredentials>(e =>
        {
            e.ToTable("ConfluenceCredentials");
            e.HasKey(x => x.Id);
            e.Property(x => x.Domain).IsRequired().HasMaxLength(256);
            e.Property(x => x.Email).IsRequired().HasMaxLength(256);
            e.HasIndex(x => x.IsActive);
        });

        modelBuilder.Entity<ConfluenceSyncedSpace>(e =>
        {
            e.ToTable("ConfluenceSyncedSpaces");
            e.HasKey(x => x.Id);
            e.Property(x => x.SpaceId).IsRequired().HasMaxLength(64);
            e.Property(x => x.SpaceKey).IsRequired().HasMaxLength(64);
            e.Property(x => x.SpaceName).IsRequired().HasMaxLength(256);
            e.Property(x => x.LocalPath).IsRequired().HasMaxLength(1024);
            e.HasIndex(x => x.SpaceId).IsUnique();
            e.HasMany(x => x.SyncedPages)
             .WithOne(x => x.SyncedSpace)
             .HasForeignKey(x => x.SyncedSpaceId)
             .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ConfluenceSyncedPage>(e =>
        {
            e.ToTable("ConfluenceSyncedPages");
            e.HasKey(x => x.Id);
            e.Property(x => x.PageId).IsRequired().HasMaxLength(64);
            e.Property(x => x.Title).IsRequired().HasMaxLength(512);
            e.Property(x => x.LocalPath).IsRequired().HasMaxLength(1024);
            e.HasIndex(x => new { x.SyncedSpaceId, x.PageId }).IsUnique();
            e.HasIndex(x => x.LocalPath).IsUnique();
        });
    }
}
