using Microsoft.EntityFrameworkCore;
using Truman.Data;
using Testcontainers.PostgreSql;

namespace Truman.Api.Tests;

public class DatabaseFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _pgContainer;
    public DbContextOptions<TrumanDbContext> DbOptions { get; private set; } = null!;

    public DatabaseFixture()
    {
        _pgContainer = new PostgreSqlBuilder()
            .WithImage("postgres:16-alpine")
            .WithDatabase("truman_test")
            .WithUsername("postgres")
            .WithPassword("postgres")
            .Build();
    }

    public async ValueTask InitializeAsync()
    {
        await _pgContainer.StartAsync();
        var connStr = _pgContainer.GetConnectionString();

        var builder = new DbContextOptionsBuilder<TrumanDbContext>()
            .UseNpgsql(connStr, opt => opt.MigrationsAssembly(typeof(TrumanDbContext).Assembly.FullName));

        DbOptions = builder.Options;

        await using var context = new TrumanDbContext(DbOptions);
        await context.Database.MigrateAsync();
    }

    public async ValueTask DisposeAsync()
    {
        await _pgContainer.DisposeAsync();
    }
}

[CollectionDefinition("Database collection", DisableParallelization = true)]
public class DatabaseCollection : ICollectionFixture<DatabaseFixture> { }
