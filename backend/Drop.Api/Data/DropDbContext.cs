using Drop.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace Drop.Api.Data;

public class DropDbContext(DbContextOptions<DropDbContext> options) : DbContext(options)
{
    public DbSet<Clip> Clips => Set<Clip>();
    public DbSet<Collection> Collections => Set<Collection>();
    public DbSet<CollectionClip> CollectionClips => Set<CollectionClip>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Clip>(entity =>
        {
            entity.HasKey(c => c.Id);
            entity.Property(c => c.Title).HasMaxLength(250).IsRequired();
            entity.Property(c => c.Description).HasMaxLength(2000);
            entity.Property(c => c.OriginalFileName).HasMaxLength(500);
            entity.Property(c => c.StoredFileName).HasMaxLength(500);
            entity.Property(c => c.ContentType).HasMaxLength(100);
            entity.Property(c => c.ThumbnailFileName).HasMaxLength(500);
            entity.Property(c => c.Visibility).HasMaxLength(20).HasDefaultValue("public");
            entity.Property(c => c.ParentClipId).HasMaxLength(50);

            entity.HasOne(c => c.ParentClip)
                .WithMany(c => c.ChildClips)
                .HasForeignKey(c => c.ParentClipId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasIndex(c => c.ParentClipId);
            entity.HasIndex(c => c.CreatedAtUtc);
            entity.HasIndex(c => c.Visibility);
        });

        modelBuilder.Entity<Collection>(entity =>
        {
            entity.HasKey(c => c.Id);
            entity.Property(c => c.Name).HasMaxLength(200).IsRequired();
            entity.Property(c => c.Description).HasMaxLength(1000);
            entity.Property(c => c.Visibility).HasMaxLength(20).HasDefaultValue("public");

            entity.HasIndex(c => c.CreatedAtUtc);
            entity.HasIndex(c => c.Visibility);
        });

        modelBuilder.Entity<CollectionClip>(entity =>
        {
            entity.HasKey(cc => new { cc.CollectionId, cc.ClipId });

            entity.HasOne(cc => cc.Collection)
                .WithMany(c => c.CollectionClips)
                .HasForeignKey(cc => cc.CollectionId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(cc => cc.Clip)
                .WithMany(c => c.CollectionClips)
                .HasForeignKey(cc => cc.ClipId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(cc => cc.OrderIndex);
        });
    }
}
