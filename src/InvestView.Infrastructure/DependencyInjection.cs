using InvestView.Application.Abstractions.MarketData;
using InvestView.Infrastructure.MarketData;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace InvestView.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        Action<MarketDataCacheOptions>? configureMarketDataCache = null)
    {
        services.AddMemoryCache();

        if (configureMarketDataCache is null)
        {
            services.AddOptions<MarketDataCacheOptions>();
        }
        else
        {
            services.Configure(configureMarketDataCache);
        }

        services.AddSingleton<MockMarketDataProvider>();
        services.AddSingleton<IMarketDataProvider>(serviceProvider =>
            new CachedMarketDataProvider(
                serviceProvider.GetRequiredService<MockMarketDataProvider>(),
                serviceProvider.GetRequiredService<Microsoft.Extensions.Caching.Memory.IMemoryCache>(),
                serviceProvider.GetRequiredService<IOptions<MarketDataCacheOptions>>(),
                serviceProvider.GetRequiredService<ILogger<CachedMarketDataProvider>>()));

        return services;
    }
}
