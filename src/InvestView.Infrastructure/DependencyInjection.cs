using InvestView.Application.Abstractions.MarketData;
using InvestView.Application.Abstractions.Realtime;
using InvestView.Infrastructure.Data;
using InvestView.Infrastructure.Dnse;
using InvestView.Infrastructure.MarketData;
using InvestView.Infrastructure.Realtime;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace InvestView.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration? configuration = null)
    {
        var marketStateOptions = new MarketStateOptions();
        configuration?.GetSection(MarketStateOptions.SectionName).Bind(marketStateOptions);
        marketStateOptions.RedisConnectionString = FirstConfiguredValue(
            marketStateOptions.RedisConnectionString,
            Environment.GetEnvironmentVariable("REDIS_CONNECTION_STRING"));
        if (string.IsNullOrWhiteSpace(marketStateOptions.RedisConnectionString))
        {
            throw new InvalidOperationException(
                "MarketData:State:RedisConnectionString is required. Configure MarketData:State:RedisConnectionString or REDIS_CONNECTION_STRING.");
        }

        services.AddLogging();
        services.AddSingleton(TimeProvider.System);

        services.AddOptions<MarketStateOptions>();

        if (configuration is null)
        {
            services.AddOptions<MarketDataProviderOptions>();
            services.AddOptions<DnseMarketDataOptions>();
            services.AddOptions<MarketQuoteStreamOptions>();
            services.AddOptions<SecurityDefinitionWarmupOptions>();
        }
        else
        {
            services.Configure<MarketStateOptions>(
                configuration.GetSection(MarketStateOptions.SectionName));
            services.Configure<MarketDataProviderOptions>(
                configuration.GetSection(MarketDataProviderOptions.SectionName));
            services.Configure<DnseMarketDataOptions>(
                configuration.GetSection(DnseMarketDataOptions.SectionName));
            services.Configure<MarketQuoteStreamOptions>(
                configuration.GetSection(MarketQuoteStreamOptions.SectionName));
            services.Configure<SecurityDefinitionWarmupOptions>(
                configuration.GetSection(SecurityDefinitionWarmupOptions.SectionName));
        }

        services.PostConfigure<DnseMarketDataOptions>(options =>
        {
            options.ApiKey = FirstConfiguredValue(options.ApiKey, Environment.GetEnvironmentVariable("DNSE_API_KEY"));
            options.ApiSecret = FirstConfiguredValue(options.ApiSecret, Environment.GetEnvironmentVariable("DNSE_API_SECRET"));
        });

        services.PostConfigure<MarketStateOptions>(options =>
        {
            options.RedisConnectionString = FirstConfiguredValue(
                options.RedisConnectionString,
                Environment.GetEnvironmentVariable("REDIS_CONNECTION_STRING"));
        });

        var databaseConnectionString = FirstConfiguredValue(
            configuration?.GetConnectionString("InvestViewDb") ?? string.Empty,
            Environment.GetEnvironmentVariable("INVESTVIEW_DB_CONNECTION_STRING"));
        if (string.IsNullOrWhiteSpace(databaseConnectionString))
        {
            databaseConnectionString = InvestViewDbContextFactory.DefaultConnectionString;
        }

        services.AddDbContext<InvestViewDbContext>(options =>
            options.UseSqlServer(databaseConnectionString));

        services.AddSingleton<MockMarketDataProvider>();
        services.AddSingleton<DnseRestSigner>();
        services.AddSingleton<DnseWebSocketAuthSigner>();
        services.AddSingleton<DnseWebSocketMessageMapper>();
        services.AddSingleton<DnseQuoteUpdateAggregator>();
        services.AddSingleton<IMarketStateMirror>(_ => new InMemoryMarketStateStore());
        services.AddSingleton<IMarketStateEventHandler, MarketStateEventSubscriber>();
        services.AddSingleton<IConnectionMultiplexer>(_ =>
            ConnectionMultiplexer.Connect(marketStateOptions.RedisConnectionString));
        services.AddSingleton<IMarketStateStore, RedisMarketStateStore>();
        services.AddSingleton<IMarketStateEventBus, RedisMarketStateEventBus>();
        services.AddHostedService<RedisMarketStateSubscriberService>();
        services.AddSingleton<IMarketStateEventPublisher, MarketStateEventPublisher>();
        services.AddSingleton<MarketQuoteStreamSchedule>();
        services.AddSingleton<SecurityDefinitionWarmupSchedule>();
        services.AddSingleton<SecurityDefinitionWarmupSymbolResolver>();
        services.AddSingleton<IMarketQuoteSubscriptionRegistry, MarketQuoteSubscriptionRegistry>();
        services.AddHttpClient<IDnseMarketDataClient, DnseMarketDataClient>();
        services.AddSingleton<DnseMarketDataProvider>();
        services.AddSingleton<MockQuoteStreamPublisher>();
        services.AddHostedService<MockQuoteStreamService>();
        services.AddHostedService<DnseWebSocketQuoteStreamService>();
        services.AddHostedService<SecurityDefinitionWarmupService>();
        services.AddSingleton<IMarketDataProvider>(serviceProvider =>
        {
            var innerProvider = ResolveInnerMarketDataProvider(serviceProvider);
            return new MarketStateBackedMarketDataProvider(
                innerProvider,
                serviceProvider.GetRequiredService<IMarketStateMirror>(),
                serviceProvider.GetRequiredService<IMarketStateStore>(),
                serviceProvider.GetRequiredService<ILogger<MarketStateBackedMarketDataProvider>>(),
                innerProvider as ISymbolMetadataProvider);
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
