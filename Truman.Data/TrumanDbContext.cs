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
    public DbSet<UserProfile> UserProfiles { get; set; } = null!;
    public DbSet<Presenter> Presenters { get; set; } = null!;
    public DbSet<ArticlePresenter> ArticlePresenters { get; set; } = null!;

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
            entity.Property(e => e.Sentiment).IsRequired();
            entity.Property(e => e.Tags).HasColumnType("jsonb"); // Store as JSON in PostgreSQL
            entity.Property(e => e.CreatedAt).IsRequired();
            
            // Configure relationship with RssItem
            entity.HasOne(e => e.RssItem)
                  .WithMany()
                  .HasForeignKey(e => e.RssItemId)
                  .OnDelete(DeleteBehavior.Cascade);
            
            // Configure relationship with ArticlePresenters
            entity.HasMany(e => e.ArticlePresenters)
                  .WithOne(e => e.Article)
                  .HasForeignKey(e => e.ArticleId)
                  .OnDelete(DeleteBehavior.Cascade);
        });
        
        modelBuilder.Entity<Presenter>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.PresenterStyle).IsUnique();
            entity.Property(e => e.PresenterStyle).IsRequired();
            entity.Property(e => e.Label).IsRequired();
        });
        
        modelBuilder.Entity<ArticlePresenter>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => new { e.ArticleId, e.PresenterId }).IsUnique();
            entity.Property(e => e.Title).IsRequired();
            entity.Property(e => e.Tldr).IsRequired();
            entity.Property(e => e.Content).IsRequired();
            
            // Configure relationship with Presenter
            entity.HasOne(e => e.Presenter)
                  .WithMany()
                  .HasForeignKey(e => e.PresenterId)
                  .OnDelete(DeleteBehavior.Cascade);
        });
    }
} 