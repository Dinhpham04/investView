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
            return Task.CompletedTask;
        }

        public Task BroadcastStreamStatusAsync(QuoteStreamStatusDto status, CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }
    }
}
