using InvestView.Application.Abstractions.Realtime;
using InvestView.Application.Dtos.MarketData;
using InvestView.Application.Dtos.Realtime;
using InvestView.Infrastructure.MarketData;
using InvestView.Infrastructure.Realtime;
using Microsoft.Extensions.Logging.Abstractions;

namespace InvestView.Api.Tests.Realtime;

public sealed class MarketStateEventPipelineTests
{
    [Fact]
    public async Task PublishQuoteUpdateAsync_WritesSharedState_UpdatesLocalMirror_AndBroadcasts()
    {
        var sharedStore = new InMemoryMarketStateStore();
        var localMirror = new InMemoryMarketStateStore();
        var broadcaster = new RecordingQuoteBroadcaster();
        var subscriber = new MarketStateEventSubscriber(
            localMirror,
            sharedStore,
            broadcaster,
            NullLogger<MarketStateEventSubscriber>.Instance);
        var publisher = new MarketStateEventPublisher(
            sharedStore,
            new InProcessMarketStateEventBus([subscriber]));

        var update = new MarketQuoteUpdateDto(
            "ssi",
            "g1",
            LastPrice: 27.1m,
            Change: 0.4m,
            ChangePercent: 1.5m,
            LastQuantity: 500,
            TotalVolume: 10_500,
            TotalValue: 284_550_000m,
            ForeignBuyVolume: null,
            ForeignSellVolume: null,
            ForeignRoom: null,
            BidLevels: null,
            AskLevels: null,
            TradingStatus: "NO_HALT",
            UpdatedAt: new DateTimeOffset(2026, 7, 8, 3, 30, 0, TimeSpan.Zero),
            ReferencePrice: 26.7m,
            CeilingPrice: 28.55m,
            FloorPrice: 24.85m);

        await publisher.PublishQuoteUpdateAsync(update, CancellationToken.None);

        var broadcast = Assert.Single(broadcaster.QuoteUpdates);
        Assert.Equal("SSI", broadcast.Symbol);
        Assert.Equal("G1", broadcast.BoardId);
        Assert.Equal(27.1m, broadcast.LastPrice);

        var sharedQuote = Assert.Single(await sharedStore.GetQuotesAsync("G1", ["SSI"], CancellationToken.None));
        var localQuote = Assert.Single(await localMirror.GetQuotesAsync("G1", ["SSI"], CancellationToken.None));
        Assert.Equal(sharedQuote.LastPrice, localQuote.LastPrice);
        Assert.Equal(sharedQuote.ReferencePrice, localQuote.ReferencePrice);
    }

    [Fact]
    public async Task PublishTradeUpdateAsync_UpdatesLatestTradesAndBroadcastsTrade()
    {
        var sharedStore = new InMemoryMarketStateStore();
        var localMirror = new InMemoryMarketStateStore();
        var broadcaster = new RecordingQuoteBroadcaster();
        var subscriber = new MarketStateEventSubscriber(
            localMirror,
            sharedStore,
            broadcaster,
            NullLogger<MarketStateEventSubscriber>.Instance);
        var publisher = new MarketStateEventPublisher(
            sharedStore,
            new InProcessMarketStateEventBus([subscriber]));
        var tradeUpdate = new MarketTradeUpdateDto(
            "SSI",
            "G1",
            new DateTimeOffset(2026, 7, 8, 3, 31, 0, TimeSpan.Zero),
            Price: 27.2m,
            Change: 0.5m,
            ChangePercent: 1.87m,
            Quantity: 1_000,
            TotalVolume: 11_000,
            TotalValue: 299_200_000m,
            Side: "S");

        await publisher.PublishTradeUpdateAsync(tradeUpdate, CancellationToken.None);

        var broadcast = Assert.Single(broadcaster.TradeUpdates);
        Assert.Equal("SSI", broadcast.Symbol);
        Assert.Equal("S", broadcast.Side);

        var localTrade = Assert.Single(await localMirror.GetLatestTradesAsync("G1", "SSI", 10, CancellationToken.None));
        var sharedTrade = Assert.Single(await sharedStore.GetLatestTradesAsync("G1", "SSI", 10, CancellationToken.None));
        Assert.Equal(sharedTrade.Price, localTrade.Price);
        Assert.Equal(27.2m, localTrade.Price);
    }

    [Fact]
    public async Task PublishMarketIndexUpdateAsync_WhenEstimatedUpdate_UpdatesLocalMirrorAndBroadcasts()
    {
        var sharedStore = new InMemoryMarketStateStore();
        await sharedStore.UpsertMarketIndicesAsync(
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
        var localMirror = new InMemoryMarketStateStore();
        var broadcaster = new RecordingQuoteBroadcaster();
        var subscriber = new MarketStateEventSubscriber(
            localMirror,
            sharedStore,
            broadcaster,
            NullLogger<MarketStateEventSubscriber>.Instance);
        var publisher = new MarketStateEventPublisher(
            sharedStore,
            new InProcessMarketStateEventBus([subscriber]));
        var estimatedAt = new DateTimeOffset(2026, 7, 8, 3, 1, 0, TimeSpan.Zero);

        await publisher.PublishMarketIndexUpdateAsync(
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
                UpdatedAt: estimatedAt,
                EstimatedValue: 1841.5m,
                EstimatedChange: 4.5m,
                EstimatedChangePercent: 0.24m,
                EstimatedTotalVolume: 110_000,
                EstimatedTotalValue: 1_100_000m,
                EstimatedUpdatedAt: estimatedAt),
            CancellationToken.None);

        var broadcast = Assert.Single(broadcaster.MarketIndexUpdates);
        Assert.Equal("VN30", broadcast.IndexName);
        Assert.Null(broadcast.Value);
        Assert.Equal(1841.5m, broadcast.EstimatedValue);

        var localIndex = Assert.Single(await localMirror.GetMarketIndicesAsync(["VN30"], CancellationToken.None));
        Assert.Equal(1840m, localIndex.Value);
        Assert.Equal(new DateTimeOffset(2026, 7, 8, 3, 0, 0, TimeSpan.Zero), localIndex.UpdatedAt);
        Assert.Equal(1841.5m, localIndex.EstimatedValue);
        Assert.Equal(estimatedAt, localIndex.EstimatedUpdatedAt);
    }

    [Fact]
    public async Task PublishOhlcUpdateAsync_WritesSharedStateAndUpdatesLocalMirror()
    {
        var sharedStore = new InMemoryMarketStateStore();
        var localMirror = new InMemoryMarketStateStore();
        var broadcaster = new RecordingQuoteBroadcaster();
        var subscriber = new MarketStateEventSubscriber(
            localMirror,
            sharedStore,
            broadcaster,
            NullLogger<MarketStateEventSubscriber>.Instance);
        var publisher = new MarketStateEventPublisher(
            sharedStore,
            new InProcessMarketStateEventBus([subscriber]));
        var barTime = new DateTimeOffset(2026, 7, 8, 3, 30, 0, TimeSpan.Zero);
        var update = new MarketOhlcUpdateDto(
            "ssi",
            "1",
            barTime,
            Open: 26.7m,
            High: 27.1m,
            Low: 26.6m,
            Close: 27m,
            Volume: 1_000,
            Type: "stock",
            IsClosed: false,
            UpdatedAt: barTime);

        await publisher.PublishOhlcUpdateAsync(update, CancellationToken.None);

        Assert.Empty(broadcaster.QuoteUpdates);
        Assert.Empty(broadcaster.TradeUpdates);
        Assert.Empty(broadcaster.MarketIndexUpdates);
        Assert.Empty(broadcaster.OhlcUpdates);

        var localBars = await localMirror.GetOhlcBarsAsync("SSI", "1", barTime.AddMinutes(-1), barTime.AddMinutes(1), CancellationToken.None);
        var sharedBars = await sharedStore.GetOhlcBarsAsync("SSI", "1", barTime.AddMinutes(-1), barTime.AddMinutes(1), CancellationToken.None);
        Assert.Equal(27m, Assert.Single(localBars).Close);
        Assert.Equal(27m, Assert.Single(sharedBars).Close);
    }

    [Fact]
    public async Task PublishOhlcUpdateAsync_WhenIndexUpdate_BroadcastsIndexOhlc()
    {
        var sharedStore = new InMemoryMarketStateStore();
        var localMirror = new InMemoryMarketStateStore();
        var broadcaster = new RecordingQuoteBroadcaster();
        var subscriber = new MarketStateEventSubscriber(
            localMirror,
            sharedStore,
            broadcaster,
            NullLogger<MarketStateEventSubscriber>.Instance);
        var publisher = new MarketStateEventPublisher(
            sharedStore,
            new InProcessMarketStateEventBus([subscriber]));
        var barTime = new DateTimeOffset(2026, 7, 8, 3, 30, 0, TimeSpan.Zero);
        var update = new MarketOhlcUpdateDto(
            "vnindex",
            "1",
            barTime,
            Open: 1830m,
            High: 1835m,
            Low: 1829m,
            Close: 1834m,
            Volume: 1_000_000,
            Type: "index",
            IsClosed: false,
            UpdatedAt: barTime);

        await publisher.PublishOhlcUpdateAsync(update, CancellationToken.None);

        var broadcast = Assert.Single(broadcaster.OhlcUpdates);
        Assert.Equal("VNINDEX", broadcast.Symbol);
        Assert.Equal("INDEX", broadcast.Type);
        Assert.Equal("1", broadcast.Resolution);
        Assert.Equal(1834m, broadcast.Close);
    }

    [Fact]
    public async Task PublishMarketSessionUpdateAsync_WritesSharedStateAndUpdatesLocalMirror()
    {
        var sharedStore = new InMemoryMarketStateStore();
        var localMirror = new InMemoryMarketStateStore();
        var broadcaster = new RecordingQuoteBroadcaster();
        var subscriber = new MarketStateEventSubscriber(
            localMirror,
            sharedStore,
            broadcaster,
            NullLogger<MarketStateEventSubscriber>.Instance);
        var publisher = new MarketStateEventPublisher(
            sharedStore,
            new InProcessMarketStateEventBus([subscriber]));
        var updatedAt = new DateTimeOffset(2026, 7, 8, 2, 15, 0, TimeSpan.Zero);

        await publisher.PublishMarketSessionUpdateAsync(
            new MarketSessionUpdateDto("dvx", "g1", "sto", "ab2", "40", updatedAt),
            CancellationToken.None);

        var localSession = await localMirror.GetMarketSessionAsync("STO", "G1", CancellationToken.None);
        var sharedSession = await sharedStore.GetMarketSessionAsync("STO", "G1", CancellationToken.None);
        Assert.NotNull(localSession);
        Assert.NotNull(sharedSession);
        Assert.Equal("DVX", localSession.MarketId);
        Assert.Equal("DVX", sharedSession.MarketId);
        Assert.Equal("40", localSession.TradingSessionId);
        var broadcast = Assert.Single(broadcaster.MarketSessionUpdates);
        Assert.Equal("G1", broadcast.BoardId);
        Assert.Equal(MarketSessionPhases.Continuous, broadcast.Phase);
        Assert.Equal(MarketSessionSources.Realtime, broadcast.Source);
    }

    [Fact]
    public async Task PublishQuoteUpdateAsync_WhenUpdateIsStale_BroadcastsLatestStateInsteadOfStaleDelta()
    {
        var sharedStore = new InMemoryMarketStateStore();
        await sharedStore.UpsertQuotesAsync(
        [
            CreateQuote("SSI", referencePrice: 26.7m, lastPrice: 27.1m)
        ], CancellationToken.None);
        var localMirror = new InMemoryMarketStateStore();
        var broadcaster = new RecordingQuoteBroadcaster();
        var subscriber = new MarketStateEventSubscriber(
            localMirror,
            sharedStore,
            broadcaster,
            NullLogger<MarketStateEventSubscriber>.Instance);
        var publisher = new MarketStateEventPublisher(
            sharedStore,
            new InProcessMarketStateEventBus([subscriber]));

        await publisher.PublishQuoteUpdateAsync(
            new MarketQuoteUpdateDto(
                "SSI",
                "G1",
                LastPrice: 26.2m,
                Change: -0.5m,
                ChangePercent: -1.87m,
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

        var broadcast = Assert.Single(broadcaster.QuoteUpdates);
        Assert.Equal(27.1m, broadcast.LastPrice);
        Assert.Equal(new DateTimeOffset(2026, 7, 8, 3, 29, 0, TimeSpan.Zero), broadcast.UpdatedAt);
    }

    [Fact]
    public async Task PublishQuoteUpdateAsync_FansOutToMultipleLocalMirrors()
    {
        var sharedStore = new InMemoryMarketStateStore();
        var firstMirror = new InMemoryMarketStateStore();
        var secondMirror = new InMemoryMarketStateStore();
        var firstBroadcaster = new RecordingQuoteBroadcaster();
        var secondBroadcaster = new RecordingQuoteBroadcaster();
        var firstSubscriber = new MarketStateEventSubscriber(
            firstMirror,
            sharedStore,
            firstBroadcaster,
            NullLogger<MarketStateEventSubscriber>.Instance);
        var secondSubscriber = new MarketStateEventSubscriber(
            secondMirror,
            sharedStore,
            secondBroadcaster,
            NullLogger<MarketStateEventSubscriber>.Instance);
        var publisher = new MarketStateEventPublisher(
            sharedStore,
            new InProcessMarketStateEventBus([firstSubscriber, secondSubscriber]));
        var update = new MarketQuoteUpdateDto(
            "SSI",
            "G1",
            LastPrice: 27.1m,
            Change: 0.4m,
            ChangePercent: 1.5m,
            LastQuantity: 500,
            TotalVolume: 10_500,
            TotalValue: 284_550_000m,
            ForeignBuyVolume: null,
            ForeignSellVolume: null,
            ForeignRoom: null,
            BidLevels: null,
            AskLevels: null,
            TradingStatus: "NO_HALT",
            UpdatedAt: new DateTimeOffset(2026, 7, 8, 3, 30, 0, TimeSpan.Zero),
            ReferencePrice: 26.7m,
            CeilingPrice: 28.55m,
            FloorPrice: 24.85m);

        await publisher.PublishQuoteUpdateAsync(update, CancellationToken.None);

        Assert.Single(firstBroadcaster.QuoteUpdates);
        Assert.Single(secondBroadcaster.QuoteUpdates);
        var firstQuote = Assert.Single(await firstMirror.GetQuotesAsync("G1", ["SSI"], CancellationToken.None));
        var secondQuote = Assert.Single(await secondMirror.GetQuotesAsync("G1", ["SSI"], CancellationToken.None));
        Assert.Equal(firstQuote.LastPrice, secondQuote.LastPrice);
        Assert.Equal(27.1m, secondQuote.LastPrice);
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
            271_000_000m,
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

    private sealed class RecordingQuoteBroadcaster : IMarketQuoteBroadcaster
    {
        public List<MarketQuoteUpdateDto> QuoteUpdates { get; } = [];
        public List<MarketTradeUpdateDto> TradeUpdates { get; } = [];
        public List<MarketIndexUpdateDto> MarketIndexUpdates { get; } = [];
        public List<MarketOhlcUpdateDto> OhlcUpdates { get; } = [];
        public List<MarketSessionUpdateDto> MarketSessionUpdates { get; } = [];

        public Task BroadcastQuoteUpdateAsync(MarketQuoteUpdateDto update, CancellationToken cancellationToken)
        {
            QuoteUpdates.Add(update);
            return Task.CompletedTask;
        }

        public Task BroadcastTradeUpdateAsync(MarketTradeUpdateDto update, CancellationToken cancellationToken)
        {
            TradeUpdates.Add(update);
            return Task.CompletedTask;
        }

        public Task BroadcastMarketIndexUpdateAsync(MarketIndexUpdateDto update, CancellationToken cancellationToken)
        {
            MarketIndexUpdates.Add(update);
            return Task.CompletedTask;
        }

        public Task BroadcastOhlcUpdateAsync(MarketOhlcUpdateDto update, CancellationToken cancellationToken)
        {
            OhlcUpdates.Add(update);
            return Task.CompletedTask;
        }

        public Task BroadcastMarketSessionUpdateAsync(MarketSessionUpdateDto update, CancellationToken cancellationToken)
        {
            MarketSessionUpdates.Add(update);
            return Task.CompletedTask;
        }

        public Task BroadcastStreamStatusAsync(QuoteStreamStatusDto status, CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }
    }
}
