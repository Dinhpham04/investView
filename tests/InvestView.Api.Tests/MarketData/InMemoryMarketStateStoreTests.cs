using InvestView.Application.Dtos.MarketData;
using InvestView.Infrastructure.MarketData;

namespace InvestView.Api.Tests.MarketData;

public sealed class InMemoryMarketStateStoreTests
{
    [Fact]
    public async Task ApplyQuoteUpdateAsync_MergesPartialUpdateWithoutLosingSnapshotFields()
    {
        var store = new InMemoryMarketStateStore();
        await store.UpsertQuotesAsync(
        [
            CreateQuote("ssi", referencePrice: 26.7m, lastPrice: 26.8m)
        ], CancellationToken.None);

        await store.ApplyQuoteUpdateAsync(
            new MarketQuoteUpdateDto(
                "SSI",
                "G1",
                LastPrice: 27.1m,
                Change: null,
                ChangePercent: null,
                LastQuantity: 500,
                TotalVolume: null,
                TotalValue: null,
                ForeignBuyVolume: null,
                ForeignSellVolume: null,
                ForeignRoom: null,
                BidLevels: null,
                AskLevels: null,
                TradingStatus: null,
                UpdatedAt: new DateTimeOffset(2026, 7, 8, 3, 30, 0, TimeSpan.Zero)),
            CancellationToken.None);

        var quote = Assert.Single(await store.GetQuotesAsync("g1", ["ssi"], CancellationToken.None));
        Assert.Equal(26.7m, quote.ReferencePrice);
        Assert.Equal(28.569m, quote.CeilingPrice);
        Assert.Equal(24.831m, quote.FloorPrice);
        Assert.Equal(27.1m, quote.LastPrice);
        Assert.Equal(0.4m, quote.Change);
        Assert.Equal(1.50m, quote.ChangePercent);
        Assert.Equal(500, quote.LastQuantity);
        Assert.Equal(10_000, quote.TotalVolume);
        Assert.Equal(100, quote.ForeignRoom);
    }

    [Fact]
    public async Task ApplyQuoteUpdateAsync_DoesNotLetStaleUpdateOverwriteCurrentQuote()
    {
        var store = new InMemoryMarketStateStore();
        await store.UpsertQuotesAsync(
        [
            CreateQuote("SSI", referencePrice: 26.7m, lastPrice: 27.1m)
        ], CancellationToken.None);

        var returnedUpdate = await store.ApplyQuoteUpdateAsync(
            new MarketQuoteUpdateDto(
                "SSI",
                "G1",
                LastPrice: 26.2m,
                Change: null,
                ChangePercent: null,
                LastQuantity: null,
                TotalVolume: null,
                TotalValue: null,
                ForeignBuyVolume: null,
                ForeignSellVolume: null,
                ForeignRoom: null,
                BidLevels: null,
                AskLevels: null,
                TradingStatus: null,
                UpdatedAt: new DateTimeOffset(2026, 7, 8, 3, 28, 0, TimeSpan.Zero)),
            CancellationToken.None);

        var quote = Assert.Single(await store.GetQuotesAsync("G1", ["SSI"], CancellationToken.None));
        Assert.Equal(27.1m, quote.LastPrice);
        Assert.Equal(new DateTimeOffset(2026, 7, 8, 3, 29, 0, TimeSpan.Zero), quote.UpdatedAt);
        Assert.Equal(27.1m, returnedUpdate.LastPrice);
        Assert.Equal(quote.UpdatedAt, returnedUpdate.UpdatedAt);
    }

    [Fact]
    public async Task ApplyTradeUpdateAsync_StoresLatestTradeAndUpdatesQuoteLastTradeFields()
    {
        var store = new InMemoryMarketStateStore();

        await store.ApplyTradeUpdateAsync(
            new MarketTradeUpdateDto(
                "ssi",
                "g1",
                new DateTimeOffset(2026, 7, 8, 3, 31, 0, TimeSpan.Zero),
                Price: 27.2m,
                Change: 0.5m,
                ChangePercent: 1.87m,
                Quantity: 1_000,
                TotalVolume: 11_000,
                TotalValue: 299_200_000m,
                Side: "B"),
            CancellationToken.None);

        var trade = Assert.Single(await store.GetLatestTradesAsync("g1", "ssi", 10, CancellationToken.None));
        Assert.Equal("SSI", trade.Symbol);
        Assert.Equal("G1", trade.BoardId);
        Assert.Equal("B", trade.Side);
        Assert.Equal(27.2m, trade.Price);

        var quote = Assert.Single(await store.GetQuotesAsync("G1", ["SSI"], CancellationToken.None));
        Assert.Equal(27.2m, quote.LastPrice);
        Assert.Equal(1_000, quote.LastQuantity);
        Assert.Equal(11_000, quote.TotalVolume);
    }

    private static MarketQuoteDto CreateQuote(string symbol, decimal referencePrice, decimal lastPrice)
    {
        var change = lastPrice - referencePrice;
        return new MarketQuoteDto(
            symbol,
            "G1",
            "STO",
            symbol,
            referencePrice,
            referencePrice * 1.07m,
            referencePrice * 0.93m,
            lastPrice,
            change,
            Math.Round(change / referencePrice * 100m, 2, MidpointRounding.AwayFromZero),
            100,
            10_000,
            268_000_000m,
            10,
            20,
            100,
            lastPrice,
            lastPrice,
            lastPrice,
            [new PriceLevelDto(lastPrice - 0.1m, 100)],
            [new PriceLevelDto(lastPrice + 0.1m, 100)],
            "NO_HALT",
            new DateTimeOffset(2026, 7, 8, 3, 29, 0, TimeSpan.Zero));
    }
}
