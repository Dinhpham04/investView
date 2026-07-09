using InvestView.Application.Abstractions.MarketData;
using InvestView.Infrastructure.MarketData;
using InvestView.Infrastructure.Realtime;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using StackExchange.Redis;

namespace InvestView.Api.Tests;

internal static class TestMarketStateServices
{
    public static IWebHostBuilder UseInMemoryMarketStateForTests(this IWebHostBuilder builder)
    {
        return builder
            .ConfigureAppConfiguration((_, configurationBuilder) =>
                configurationBuilder.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["MarketData:State:RedisConnectionString"] = "localhost:6379"
                }))
            .ConfigureServices(services =>
            {
                RemoveHostedService<RedisMarketStateSubscriberService>(services);
                services.RemoveAll<IConnectionMultiplexer>();
                services.RemoveAll<IMarketStateStore>();
                services.RemoveAll<IMarketStateEventBus>();
                services.AddSingleton<IMarketStateStore>(_ => new InMemoryMarketStateStore());
                services.AddSingleton<IMarketStateEventBus, InProcessMarketStateEventBus>();
            });
    }

    private static void RemoveHostedService<THostedService>(IServiceCollection services)
    {
        var descriptors = services
            .Where(descriptor =>
                descriptor.ServiceType == typeof(IHostedService) &&
                descriptor.ImplementationType == typeof(THostedService))
            .ToArray();

        foreach (var descriptor in descriptors)
        {
            services.Remove(descriptor);
        }
    }
}
