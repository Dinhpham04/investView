using InvestView.Application.Abstractions.MarketData;
using InvestView.Infrastructure.Dnse;
using InvestView.Infrastructure.MarketData;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace InvestView.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration? configuration = null,
        Action<MarketDataCacheOptions>? configureMarketDataCache = null)
    {
        services.AddLogging();
        services.AddMemoryCache();

        if (configureMarketDataCache is null)
        {
            services.AddOptions<MarketDataCacheOptions>();
        }
        else
        {
            services.Configure(configureMarketDataCache);
        }

        if (configuration is null)
        {
            services.AddOptions<MarketDataProviderOptions>();
            services.AddOptions<DnseMarketDataOptions>();
        }
        else
        {
            services.Configure<MarketDataProviderOptions>(
                configuration.GetSection(MarketDataProviderOptions.SectionName));
            services.Configure<DnseMarketDataOptions>(
                configuration.GetSection(DnseMarketDataOptions.SectionName));
        }

        services.PostConfigure<DnseMarketDataOptions>(options =>
        {
            options.ApiKey = FirstConfiguredValue(options.ApiKey, Environment.GetEnvironmentVariable("DNSE_API_KEY"));
            options.ApiSecret = FirstConfiguredValue(options.ApiSecret, Environment.GetEnvironmentVariable("DNSE_API_SECRET"));
        });

        services.AddSingleton<MockMarketDataProvider>();
        services.AddSingleton<DnseRestSigner>();
        services.AddHttpClient<IDnseMarketDataClient, DnseMarketDataClient>();
        services.AddSingleton<DnseMarketDataProvider>();
        services.AddSingleton<IMarketDataProvider>(serviceProvider =>
        {
            var inner = ResolveInnerMarketDataProvider(serviceProvider);

            return new CachedMarketDataProvider(
                inner,
                serviceProvider.GetRequiredService<Microsoft.Extensions.Caching.Memory.IMemoryCache>(),
                serviceProvider.GetRequiredService<IOptions<MarketDataCacheOptions>>(),
                serviceProvider.GetRequiredService<ILogger<CachedMarketDataProvider>>());
        });

        return services;
    }

    private static string FirstConfiguredValue(string configuredValue, string? environmentValue)
    {
        return string.IsNullOrWhiteSpace(configuredValue)
            ? environmentValue ?? string.Empty
            : configuredValue;
    }

    private static IMarketDataProvider ResolveInnerMarketDataProvider(IServiceProvider serviceProvider)
    {
        var providerOptions = serviceProvider.GetRequiredService<IOptions<MarketDataProviderOptions>>().Value;
        var dnseOptions = serviceProvider.GetRequiredService<IOptions<DnseMarketDataOptions>>().Value;
        var logger = serviceProvider
            .GetRequiredService<ILoggerFactory>()
            .CreateLogger("InvestView.Infrastructure.MarketDataProvider");

        if (providerOptions.Provider.Equals("Dnse", StringComparison.OrdinalIgnoreCase))
        {
            if (dnseOptions.HasCredentials)
            {
                logger.LogInformation("Using DNSE REST market data provider.");
                return serviceProvider.GetRequiredService<DnseMarketDataProvider>();
            }

            logger.LogWarning(
                "MarketData:Provider is Dnse but Dnse:ApiKey or Dnse:ApiSecret is missing. Falling back to mock market data.");
        }

        return serviceProvider.GetRequiredService<MockMarketDataProvider>();
    }
}
