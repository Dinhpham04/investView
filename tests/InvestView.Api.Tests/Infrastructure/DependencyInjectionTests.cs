using InvestView.Infrastructure;
using InvestView.Infrastructure.MarketData;
using InvestView.Infrastructure.Realtime;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace InvestView.Api.Tests.Infrastructure;

public sealed class DependencyInjectionTests
{
    [Fact]
    public void AddInfrastructure_WhenMarketDataCacheIsConfigured_BindsCacheOptions()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["MarketData:Provider"] = "Mock",
                ["MarketData:Cache:MarketBoardTtl"] = "7.00:00:00",
                ["MarketData:Cache:SymbolDetailTtl"] = "00:30:00",
                ["MarketData:Cache:OhlcTtl"] = "00:05:00"
            })
            .Build();
        var services = new ServiceCollection();

        services.AddInfrastructure(configuration);

        using var serviceProvider = services.BuildServiceProvider();
        var options = serviceProvider.GetRequiredService<IOptions<MarketDataCacheOptions>>().Value;
        Assert.Equal(TimeSpan.FromDays(7), options.MarketBoardTtl);
        Assert.Equal(TimeSpan.FromMinutes(30), options.SymbolDetailTtl);
        Assert.Equal(TimeSpan.FromMinutes(5), options.OhlcTtl);
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
}
