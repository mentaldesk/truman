using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Npgsql;
using Truman.Data;

namespace Truman.JobRunner;

public class DbMigrator(
    ILogger<DbMigrator> logger,
    IDbContextFactory<TrumanDbContext> dbFactory,
    int maxAttempts = 30,
    TimeSpan? retryDelay = null)
{
    private readonly TimeSpan _delay = retryDelay ?? TimeSpan.FromSeconds(2);

    public async Task RunAsync(CancellationToken cancellationToken = default)
    {
        logger.LogInformation("Starting database migrations...");
        for (int attempt = 1; attempt <= maxAttempts; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
                await db.Database.MigrateAsync(cancellationToken);
                logger.LogInformation("Migrations completed successfully");
                return;
            }
            catch (NpgsqlException ex) when (attempt < maxAttempts)
            {
                logger.LogWarning(ex, "Attempt {Attempt}/{Max} - database not ready yet. Retrying in {Delay}...", attempt, maxAttempts, _delay);
                await Task.Delay(_delay, cancellationToken);
            }
            catch (Exception ex) when (attempt < maxAttempts)
            {
                logger.LogWarning(ex, "Attempt {Attempt}/{Max} failed. Retrying in {Delay}...", attempt, maxAttempts, _delay);
                await Task.Delay(_delay, cancellationToken);
            }
        }
        logger.LogError("Exceeded maximum attempts ({Max}) waiting for database to be ready for migrations", maxAttempts);
        throw new InvalidOperationException("Database migrations failed after maximum retry attempts.");
    }
}

