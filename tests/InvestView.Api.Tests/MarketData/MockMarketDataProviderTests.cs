using InvestView.Infrastructure.MarketData;

namespace InvestView.Api.Tests.MarketData;

public sealed class MockMarketDataProviderTests
{
    [Fact]
    public async Task GetMarketBoardAsync_ReturnsStableProviderNeutralQuotes()
    {
        var provider = new MockMarketDataProvider();

        var quotes = await provider.GetMarketBoardAsync([], "G1", CancellationToken.None);

        Assert.Equal(["HPG", "SSI", "VCB"], quotes.Select(quote => quote.Symbol));
        Assert.All(quotes, quote =>
        {
            Assert.Equal("G1", quote.BoardId);
            Assert.Equal("Continuous", quote.TradingStatus);
            Assert.Equal(new DateTimeOffset(2026, 7, 3, 7, 45, 0, TimeSpan.Zero), quote.UpdatedAt);
            Assert.True(quote.ForeignBuyVolume > 0);
            Assert.True(quote.ForeignSellVolume > 0);
            Assert.True(quote.ForeignRoom > 0);
            Assert.Equal(3, quote.BidLevels.Count);
            Assert.Equal(3, quote.AskLevels.Count);
            Assert.All(quote.BidLevels.Concat(quote.AskLevels), level => Assert.True(level.Quantity > 0));
        });
    }

    [Fact]
    public async Task GetMarketBoardAsync_FiltersSymbolsCaseInsensitively()
    {
        var provider = new MockMarketDataProvider();

        var quotes = await provider.GetMarketBoardAsync(["ssi"], "g1", CancellationToken.None);

        var quote = Assert.Single(quotes);
        Assert.Equal("SSI", quote.Symbol);
        Assert.True(quote.Change < 0);
        Assert.Equal(-0.99m, quote.ChangePercent);
    }

    [Fact]
    public async Task GetSymbolDetailAsync_MapsQuoteReferenceData()
    {
        var provider = new MockMarketDataProvider();

        var detail = await provider.GetSymbolDetailAsync("hpg", CancellationToken.None);

        Assert.NotNull(detail);
        Assert.Equal("HPG", detail.Symbol);
        Assert.Equal("G1", detail.BoardId);
        Assert.Equal(28600m, detail.ReferencePrice);
        Assert.Equal(30600m, detail.CeilingPrice);
        Assert.Equal(26600m, detail.FloorPrice);
    }

    [Fact]
    public async Task GetOhlcAsync_ReturnsStableBarsForKnownSymbol()
    {
        var provider = new MockMarketDataProvider();

        var bars = await provider.GetOhlcAsync("HPG", "1", CancellationToken.None);

        Assert.Equal(3, bars.Count);
        Assert.All(bars, bar =>
        {
            Assert.Equal("HPG", bar.Symbol);
            Assert.Equal("1", bar.Resolution);
            Assert.True(bar.Volume > 0);
        });
    }
}
