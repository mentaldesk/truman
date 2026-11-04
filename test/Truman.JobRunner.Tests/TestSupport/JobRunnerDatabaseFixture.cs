using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;
using Truman.Data;
using Truman.Data.Entities;

namespace Truman.JobRunner.Tests.TestSupport;

/// Postgres rather than the InMemory provider because the save and failure paths open transactions.
public class JobRunnerDatabaseFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .WithDatabase("truman_jobrunner_test")
        .WithUsername("postgres")
        .WithPassword("postgres")
        .Build();

    public IDbContextFactory<TrumanDbContext> ContextFactory { get; private set; } = null!;

    private DbContextOptions<TrumanDbContext> _options = null!;

    public async ValueTask InitializeAsync()
    {
        await _container.StartAsync();

        _options = new DbContextOptionsBuilder<TrumanDbContext>()
            .UseNpgsql(_container.GetConnectionString(),
                opt => opt.MigrationsAssembly(typeof(TrumanDbContext).Assembly.FullName))
            .Options;
        ContextFactory = new TestDbContextFactory(_options);

        await using var db = new TrumanDbContext(_options);
        await db.Database.MigrateAsync();
    }

    public async ValueTask DisposeAsync() => await _container.DisposeAsync();

    public TrumanDbContext NewContext() => new(_options);

    public async Task ResetAsync()
    {
        await using var db = NewContext();
        await db.Database.ExecuteSqlRawAsync(
            """TRUNCATE "ArticlePresenters", "Articles", "RssItems", "Presenters" RESTART IDENTITY CASCADE;""");
    }

    public async Task<RssItem> SeedPendingAsync(string link, string title = "A headline", string description = "Body text.")
    {
        await using var db = NewContext();
        var item = new RssItem { Link = link, Title = title, Description = description };
        db.RssItems.Add(item);
        await db.SaveChangesAsync();
        return item;
    }

    public async Task<Presenter> SeedPresenterAsync(string style = "a terse newsreader", string label = "Terse")
    {
        await using var db = NewContext();
        var presenter = new Presenter { PresenterStyle = style, Label = label };
        db.Presenters.Add(presenter);
        await db.SaveChangesAsync();
        return presenter;
    }

    private sealed class TestDbContextFactory(DbContextOptions<TrumanDbContext> options)
        : IDbContextFactory<TrumanDbContext>
    {
        public TrumanDbContext CreateDbContext() => new(options);
    }
}

[CollectionDefinition(Name, DisableParallelization = true)]
public class JobRunnerDatabaseCollection : ICollectionFixture<JobRunnerDatabaseFixture>
{
    public const string Name = "JobRunner database collection";
}
