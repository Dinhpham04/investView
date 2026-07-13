using InvestView.Domain.Trading;
using InvestView.Infrastructure;
using InvestView.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace InvestView.Api.Tests.Infrastructure;

public sealed class InvestViewDbContextTests
{
    [Fact]
    public void Model_ContainsCoreTradingEntities()
    {
        using var dbContext = CreateDbContext();

        Assert.NotNull(dbContext.Model.FindEntityType(typeof(UserAccount)));
        Assert.NotNull(dbContext.Model.FindEntityType(typeof(WatchlistGroup)));
        Assert.NotNull(dbContext.Model.FindEntityType(typeof(WatchlistItem)));
        Assert.NotNull(dbContext.Model.FindEntityType(typeof(CashAccount)));
        Assert.NotNull(dbContext.Model.FindEntityType(typeof(Holding)));
        Assert.NotNull(dbContext.Model.FindEntityType(typeof(SimulatedOrder)));
        Assert.NotNull(dbContext.Model.FindEntityType(typeof(OrderExecution)));
    }

    [Fact]
    public void Model_HasExpectedUniqueIndexes()
    {
        using var dbContext = CreateDbContext();

        AssertHasUniqueIndex<UserAccount>(dbContext, nameof(UserAccount.Email));
        AssertHasUniqueIndex<WatchlistGroup>(
            dbContext,
            nameof(WatchlistGroup.UserId),
            nameof(WatchlistGroup.Name));
        AssertHasUniqueIndex<WatchlistItem>(
            dbContext,
            nameof(WatchlistItem.GroupId),
            nameof(WatchlistItem.BoardId),
            nameof(WatchlistItem.Symbol));
        AssertHasUniqueIndex<CashAccount>(
            dbContext,
            nameof(CashAccount.UserId),
            nameof(CashAccount.Currency));
        AssertHasUniqueIndex<Holding>(
            dbContext,
            nameof(Holding.UserId),
            nameof(Holding.BoardId),
            nameof(Holding.Symbol));
    }

    [Fact]
    public void Model_HasExpectedRequiredCascadeRelationships()
    {
        using var dbContext = CreateDbContext();

        AssertHasRequiredCascadeForeignKey<WatchlistGroup>(
            dbContext,
            typeof(UserAccount),
            nameof(WatchlistGroup.UserId));
        AssertHasRequiredCascadeForeignKey<WatchlistItem>(
            dbContext,
            typeof(WatchlistGroup),
            nameof(WatchlistItem.GroupId));
        AssertHasRequiredCascadeForeignKey<CashAccount>(
            dbContext,
            typeof(UserAccount),
            nameof(CashAccount.UserId));
        AssertHasRequiredCascadeForeignKey<Holding>(
            dbContext,
            typeof(UserAccount),
            nameof(Holding.UserId));
        AssertHasRequiredCascadeForeignKey<SimulatedOrder>(
            dbContext,
            typeof(UserAccount),
            nameof(SimulatedOrder.UserId));
        AssertHasRequiredCascadeForeignKey<OrderExecution>(
            dbContext,
            typeof(SimulatedOrder),
            nameof(OrderExecution.OrderId));
    }

    [Fact]
    public void AddInfrastructure_RegistersInvestViewDbContextWithSqlServerProvider()
    {
        var services = new ServiceCollection();
        services.AddInfrastructure(CreateConfiguration());

        using var serviceProvider = services.BuildServiceProvider();
        using var scope = serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<InvestViewDbContext>();

        Assert.Equal("Microsoft.EntityFrameworkCore.SqlServer", dbContext.Database.ProviderName);
    }

    [Fact]
    public void Migrations_IncludeWatchlistGroupsMigration()
    {
        using var dbContext = CreateDbContext();

        Assert.Contains("20260713150000_AddWatchlistGroups", dbContext.Database.GetMigrations());
    }

    private static InvestViewDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<InvestViewDbContext>()
            .UseSqlServer(InvestViewDbContextFactory.DefaultConnectionString)
            .Options;

        return new InvestViewDbContext(options);
    }

    private static IConfiguration CreateConfiguration()
    {
        return new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["MarketData:State:RedisConnectionString"] = "localhost:6379",
                ["ConnectionStrings:InvestViewDb"] = InvestViewDbContextFactory.DefaultConnectionString
            })
            .Build();
    }

    private static void AssertHasUniqueIndex<TEntity>(DbContext dbContext, params string[] propertyNames)
    {
        var entityType = dbContext.Model.FindEntityType(typeof(TEntity));
        Assert.NotNull(entityType);

        Assert.Contains(
            entityType.GetIndexes(),
            index => index.IsUnique && index.Properties.Select(property => property.Name).SequenceEqual(propertyNames));
    }

    private static void AssertHasRequiredCascadeForeignKey<TEntity>(
        DbContext dbContext,
        Type principalType,
        params string[] propertyNames)
    {
        var entityType = dbContext.Model.FindEntityType(typeof(TEntity));
        Assert.NotNull(entityType);

        var foreignKey = Assert.Single(
            entityType.GetForeignKeys(),
            key => key.PrincipalEntityType.ClrType == principalType
                && key.Properties.Select(property => property.Name).SequenceEqual(propertyNames));
        Assert.True(foreignKey.IsRequired);
        Assert.Equal(DeleteBehavior.Cascade, foreignKey.DeleteBehavior);
    }
}
