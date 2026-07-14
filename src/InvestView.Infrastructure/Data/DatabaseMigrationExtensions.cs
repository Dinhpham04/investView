using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace InvestView.Infrastructure.Data;

public static class DatabaseMigrationExtensions
{
    public static async Task MigrateDatabaseAsync(
        this IServiceProvider services,
        CancellationToken cancellationToken = default)
    {
        await using var scope = services.CreateAsyncScope();
        var options = scope.ServiceProvider.GetRequiredService<IOptions<DatabaseMigrationOptions>>().Value;
        var logger = scope.ServiceProvider
            .GetRequiredService<ILoggerFactory>()
            .CreateLogger("InvestView.Infrastructure.Data.DatabaseMigration");

        if (!options.ApplyMigrationsOnStartup)
        {
            logger.LogInformation("Database migrations on startup are disabled.");
            return;
        }

        var dbContext = scope.ServiceProvider.GetRequiredService<InvestViewDbContext>();
        if (!dbContext.Database.IsRelational())
        {
            logger.LogInformation(
                "Skipping database migrations because provider {ProviderName} is not relational.",
                dbContext.Database.ProviderName ?? "unknown");
            return;
        }

        var strategy = dbContext.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            var pendingMigrations = (await dbContext.Database
                    .GetPendingMigrationsAsync(cancellationToken))
                .ToArray();

            if (pendingMigrations.Length == 0)
            {
                logger.LogInformation("No pending database migrations.");
            }
            else
            {
                logger.LogInformation(
                    "Applying {MigrationCount} pending database migration(s): {Migrations}.",
                    pendingMigrations.Length,
                    string.Join(", ", pendingMigrations));
            }

            await dbContext.Database.MigrateAsync(cancellationToken);
            logger.LogInformation("Database migration check completed.");
        });
    }
}
