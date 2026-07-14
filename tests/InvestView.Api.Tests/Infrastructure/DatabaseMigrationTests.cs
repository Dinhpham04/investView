using InvestView.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace InvestView.Api.Tests.Infrastructure;

public sealed class DatabaseMigrationTests
{
    [Fact]
    public async Task MigrateDatabaseAsync_WhenStartupMigrationsAreDisabled_DoesNotRequireDbContext()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.Configure<DatabaseMigrationOptions>(options =>
            options.ApplyMigrationsOnStartup = false);

        await using var serviceProvider = services.BuildServiceProvider();

        await serviceProvider.MigrateDatabaseAsync();
    }

    [Fact]
    public async Task MigrateDatabaseAsync_WhenProviderIsNotRelational_SkipsMigration()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.Configure<DatabaseMigrationOptions>(options =>
            options.ApplyMigrationsOnStartup = true);
        services.AddDbContext<InvestViewDbContext>(options =>
            options.UseInMemoryDatabase(Guid.NewGuid().ToString()));

        await using var serviceProvider = services.BuildServiceProvider();

        await serviceProvider.MigrateDatabaseAsync();
    }
}
