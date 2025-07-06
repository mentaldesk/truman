using Microsoft.EntityFrameworkCore;
using Truman.Data.Entities;
using Npgsql;

namespace Truman.Data;

public class TrumanDbContext : DbContext
{
    public TrumanDbContext(DbContextOptions<TrumanDbContext> options)
        : base(options)
    {
    }

    public DbSet<MagicLink> MagicLinks { get; set; } = null!;
    public DbSet<RssItem> RssItems { get; set; } = null!;
    public DbSet<Article> Articles { get; set; } = null!;

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        base.OnConfiguring(optionsBuilder);
        
        // Enable dynamic JSON serialization for Npgsql
        NpgsqlConnection.GlobalTypeMapper.EnableDynamicJson();
    }

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

        modelBuilder.Entity<Article>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.Link).IsUnique();
            entity.Property(e => e.Link).IsRequired();
            entity.Property(e => e.Title).IsRequired();
            entity.Property(e => e.Tldr).IsRequired();
            entity.Property(e => e.Content).IsRequired();
            entity.Property(e => e.Sentiment).IsRequired();
            entity.Property(e => e.Tags).HasColumnType("jsonb"); // Store as JSON in PostgreSQL
            entity.Property(e => e.CreatedAt).IsRequired();
            
            // Configure relationship with RssItem
            entity.HasOne(e => e.RssItem)
                  .WithMany()
                  .HasForeignKey(e => e.RssItemId)
                  .OnDelete(DeleteBehavior.Cascade);
        });
    }
} 