using InvestView.Application.Abstractions.MarketData;
using InvestView.Application.Abstractions.Realtime;
using InvestView.Application.Dtos.MarketData;
using InvestView.Application.Dtos.Realtime;
using InvestView.Infrastructure;
using InvestView.Infrastructure.MarketData;
using InvestView.Infrastructure.Realtime;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using StackExchange.Redis;

namespace InvestView.Api.Tests.Infrastructure;

public sealed class DependencyInjectionTests
{
    [Fact]
    public void AddInfrastructure_DoesNotRegisterMemoryCacheForMarketData()
    {
        var services = new ServiceCollection();
        var memoryCachingNamespace = string.Join(".", "Microsoft", "Extensions", "Caching", "Memory");

        services.AddInfrastructure(CreateRedisConfiguration());

        Assert.DoesNotContain(services, descriptor =>
            descriptor.ServiceType.Namespace == memoryCachingNamespace);
    }

    [Theory]
    [InlineData(MarketQuoteStreamOptions.MockSourceProvider, true, false)]
    [InlineData(MarketQuoteStreamOptions.ConfiguredSourceProvider, true, false)]
    [InlineData(MarketQuoteStreamOptions.DnseWebSocketSourceProvider, false, true)]
    public void MarketQuoteStreamOptions_ClassifiesRealtimeSourceProviders(
        string sourceProvider,
        bool usesMock,
        bool usesDnseWebSocket)
    {
        var options = new MarketQuoteStreamOptions { SourceProvider = sourceProvider };

        Assert.Equal(usesMock, options.UsesMockCompatibleSourceProvider());
        Assert.Equal(usesDnseWebSocket, options.UsesDnseWebSocketSourceProvider());
    }

    [Fact]
    public void AddInfrastructure_RegistersMarketQuoteSubscriptionRegistry()
    {
        var services = new ServiceCollection();

        services.AddInfrastructure(CreateRedisConfiguration());

        using var serviceProvider = services.BuildServiceProvider();
        var registry = serviceProvider.GetRequiredService<IMarketQuoteSubscriptionRegistry>();
        Assert.IsType<MarketQuoteSubscriptionRegistry>(registry);
    }

    [Fact]
    public void AddInfrastructure_RegistersMarketQuoteStreamSchedule()
    {
        var services = new ServiceCollection();

        services.AddInfrastructure(CreateRedisConfiguration());

        using var serviceProvider = services.BuildServiceProvider();
        var schedule = serviceProvider.GetRequiredService<MarketQuoteStreamSchedule>();
        Assert.NotNull(schedule);
    }

    [Fact]
    public void AddInfrastructure_RegistersRedisMarketStateServices()
    {
        var services = new ServiceCollection();

        services.AddInfrastructure(CreateRedisConfiguration());
        services.AddSingleton<IMarketQuoteBroadcaster, NoopMarketQuoteBroadcaster>();

        Assert.Contains(services, descriptor =>
            descriptor.ServiceType == typeof(IConnectionMultiplexer));
        Assert.Contains(services, descriptor =>
            descriptor.ServiceType == typeof(IMarketStateStore) &&
            descriptor.ImplementationType == typeof(RedisMarketStateStore));
        Assert.Contains(services, descriptor =>
            descriptor.ServiceType == typeof(IMarketStateEventBus) &&
            descriptor.ImplementationType == typeof(RedisMarketStateEventBus));
        Assert.Contains(services, descriptor =>
            descriptor.ServiceType == typeof(IMarketStateMirror) &&
            descriptor.ImplementationFactory is not null);
    }

    [Fact]
    public void AddInfrastructure_WhenRedisConnectionStringIsMissing_FailsFast()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["MarketData:Provider"] = "Mock"
            })
            .Build();
        var services = new ServiceCollection();

        var exception = Assert.Throws<InvalidOperationException>(() => services.AddInfrastructure(configuration));
        Assert.Contains("RedisConnectionString is required", exception.Message, StringComparison.Ordinal);
    }

    private static IConfiguration CreateRedisConfiguration()
    {
        return new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["MarketData:State:RedisConnectionString"] = "localhost:6379"
            })
            .Build();
    }

    private sealed class NoopMarketQuoteBroadcaster : IMarketQuoteBroadcaster
    {
        public Task BroadcastQuoteUpdateAsync(MarketQuoteUpdateDto update, CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }

        public Task BroadcastTradeUpdateAsync(MarketTradeUpdateDto update, CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }

        public Task BroadcastMarketIndexUpdateAsync(MarketIndexUpdateDto update, CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }

        public Task BroadcastOhlcUpdateAsync(MarketOhlcUpdateDto update, CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }

        public Task BroadcastMarketSessionUpdateAsync(MarketSessionUpdateDto update, CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }

        public Task BroadcastStreamStatusAsync(QuoteStreamStatusDto status, CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }
    }
}
