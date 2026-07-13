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
    public async Task ApplyQuoteUpdateAsync_WhenExpectedPriceUpdate_MergesWithoutOverwritingMatchedPrice()
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
                LastPrice: null,
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
                UpdatedAt: new DateTimeOffset(2026, 7, 8, 2, 15, 0, TimeSpan.Zero),
                ExpectedPrice: 27.0m,
                ExpectedQuantity: 10_000),
            CancellationToken.None);

        var quote = Assert.Single(await store.GetQuotesAsync("g1", ["ssi"], CancellationToken.None));
        Assert.Equal(26.8m, quote.LastPrice);
        Assert.Equal(new DateTimeOffset(2026, 7, 8, 3, 29, 0, TimeSpan.Zero), quote.UpdatedAt);
        Assert.Equal(27.0m, quote.ExpectedPrice);
        Assert.Equal(10_000, quote.ExpectedQuantity);
    }

    [Fact]
    public async Task ApplyQuoteUpdateAsync_WhenMatchedPriceArrives_ClearsExpectedAuctionFields()
    {
        var store = new InMemoryMarketStateStore();
        await store.UpsertQuotesAsync(
        [
            CreateQuote("ssi", referencePrice: 26.7m, lastPrice: 26.8m) with
            {
                ExpectedPrice = 27.0m,
                ExpectedQuantity = 10_000
            }
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
        Assert.Equal(27.1m, quote.LastPrice);
        Assert.Equal(500, quote.LastQuantity);
        Assert.Null(quote.ExpectedPrice);
        Assert.Null(quote.ExpectedQuantity);
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

    [Fact]
    public async Task ApplyOhlcUpdateAsync_StoresSymbolAndIndexBarsInTimeline()
    {
        var store = new InMemoryMarketStateStore();
        var stockTime = new DateTimeOffset(2026, 7, 8, 3, 30, 0, TimeSpan.Zero);
        var indexTime = stockTime.AddMinutes(1);

        await store.ApplyOhlcUpdateAsync(
            new MarketOhlcUpdateDto("ssi", "1", stockTime, 26.7m, 27.1m, 26.6m, 27m, 1_000, "stock", false, stockTime),
            CancellationToken.None);
        await store.ApplyOhlcUpdateAsync(
            new MarketOhlcUpdateDto("vn30", "1D", indexTime, 1840m, 1850m, 1838m, 1848m, 10_000, "index", true, indexTime),
            CancellationToken.None);

        var stockBars = await store.GetOhlcBarsAsync("SSI", "1", stockTime.AddMinutes(-1), stockTime.AddMinutes(1), CancellationToken.None);
        var indexBars = await store.GetIndexOhlcBarsAsync("VN30", "1D", indexTime.AddMinutes(-1), indexTime.AddMinutes(1), CancellationToken.None);

        var stockBar = Assert.Single(stockBars);
        Assert.Equal("SSI", stockBar.Symbol);
        Assert.Equal(27m, stockBar.Close);
        var indexBar = Assert.Single(indexBars);
        Assert.Equal("VN30", indexBar.Symbol);
        Assert.Equal(1848m, indexBar.Close);
    }

    [Fact]
    public async Task ApplyMarketIndexUpdateAsync_WhenEstimatedUpdate_MergesWithoutOverwritingActualIndex()
    {
        var store = new InMemoryMarketStateStore();
        await store.UpsertMarketIndicesAsync(
        [
            new MarketIndexDto(
                "VN30",
                Value: 1840m,
                Change: 3m,
                ChangePercent: 0.16m,
                ReferenceValue: 1837m,
                HighValue: 1845m,
                LowValue: 1835m,
                TotalVolume: 100_000,
                TotalValue: 1_000_000m,
                UpCount: 15,
                DownCount: 10,
                NoChangeCount: 5,
                CeilingCount: 1,
                FloorCount: 0,
                MarketId: "HOSE",
                TradingSessionId: "99",
                UpdatedAt: new DateTimeOffset(2026, 7, 8, 3, 0, 0, TimeSpan.Zero))
        ], CancellationToken.None);

        await store.ApplyMarketIndexUpdateAsync(
            new MarketIndexUpdateDto(
                "VN30",
                Value: null,
                Change: null,
                ChangePercent: null,
                ReferenceValue: null,
                HighValue: null,
                LowValue: null,
                TotalVolume: null,
                TotalValue: null,
                UpCount: null,
                DownCount: null,
                NoChangeCount: null,
                CeilingCount: null,
                FloorCount: null,
                MarketId: string.Empty,
                TradingSessionId: string.Empty,
                UpdatedAt: new DateTimeOffset(2026, 7, 8, 3, 1, 0, TimeSpan.Zero),
                EstimatedValue: 1841.5m,
                EstimatedChange: 4.5m,
                EstimatedChangePercent: 0.24m,
                EstimatedTotalVolume: 110_000,
                EstimatedTotalValue: 1_100_000m,
                EstimatedUpdatedAt: new DateTimeOffset(2026, 7, 8, 3, 1, 0, TimeSpan.Zero)),
            CancellationToken.None);

        var index = Assert.Single(await store.GetMarketIndicesAsync(["VN30"], CancellationToken.None));
        Assert.Equal(1840m, index.Value);
        Assert.Equal(new DateTimeOffset(2026, 7, 8, 3, 0, 0, TimeSpan.Zero), index.UpdatedAt);
        Assert.Equal(1841.5m, index.EstimatedValue);
        Assert.Equal(4.5m, index.EstimatedChange);
        Assert.Equal(110_000, index.EstimatedTotalVolume);
        Assert.Equal(new DateTimeOffset(2026, 7, 8, 3, 1, 0, TimeSpan.Zero), index.EstimatedUpdatedAt);
    }

    [Fact]
    public async Task ApplyMarketSessionUpdateAsync_StoresLatestSession()
    {
        var store = new InMemoryMarketStateStore();
        var updatedAt = new DateTimeOffset(2026, 7, 8, 2, 15, 0, TimeSpan.Zero);

        await store.ApplyMarketSessionUpdateAsync(
            new MarketSessionUpdateDto("dvx", "g1", "sto", "ab2", "40", updatedAt),
            CancellationToken.None);

        var session = await store.GetMarketSessionAsync("STO", "G1", CancellationToken.None);

        Assert.NotNull(session);
        Assert.Equal("DVX", session.MarketId);
        Assert.Equal("G1", session.BoardId);
        Assert.Equal("STO", session.ProductGroupId);
        Assert.Equal("AB2", session.EventId);
        Assert.Equal("40", session.TradingSessionId);
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
