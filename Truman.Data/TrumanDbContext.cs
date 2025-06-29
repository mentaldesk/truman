using Microsoft.EntityFrameworkCore;
using Truman.Data.Entities;

namespace Truman.Data;

public class TrumanDbContext : DbContext
{
    public TrumanDbContext(DbContextOptions<TrumanDbContext> options)
        : base(options)
    {
    }

    public DbSet<MagicLink> MagicLinks { get; set; } = null!;
    public DbSet<RssItem> RssItems { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<MagicLink>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.Code).IsUnique();
            entity.Property(e => e.Code).IsRequired();
            entity.Property(e => e.Email).IsRequired();
            entity.Property(e => e.ExpiresAt).IsRequired();
            entity.Property(e => e.CreatedAt)
                .IsRequired()
                .HasDefaultValueSql("CURRENT_TIMESTAMP");
        });

        modelBuilder.Entity<RssItem>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.Link).IsUnique();
            entity.Property(e => e.Link).IsRequired();
            entity.Property(e => e.PubDate).IsRequired(false);
            entity.Property(e => e.TimeAnalysed).IsRequired(false);
        });
    }
} 