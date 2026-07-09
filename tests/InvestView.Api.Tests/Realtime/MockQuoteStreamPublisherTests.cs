using InvestView.Application.Abstractions.MarketData;
using InvestView.Application.Abstractions.Realtime;
using InvestView.Application.Dtos.MarketData;
using InvestView.Application.Dtos.Realtime;
using InvestView.Infrastructure.MarketData;
using InvestView.Infrastructure.Realtime;
using Microsoft.Extensions.Options;

namespace InvestView.Api.Tests.Realtime;

public sealed class MockQuoteStreamPublisherTests
{
    [Fact]
    public async Task PublishOnceAsync_LoadsConfiguredSymbolsAndBroadcastsQuoteUpdates()
    {
        var provider = new RecordingMarketDataProvider();
        var broadcaster = new RecordingQuoteBroadcaster();
        var publisher = new MockQuoteStreamPublisher(
            provider,
            new MockMarketDataProvider(),
            broadcaster,
            Options.Create(new MarketQuoteStreamOptions
            {
                SourceProvider = MarketQuoteStreamOptions.ConfiguredSourceProvider,
                BoardId = "g1",
                Symbols = ["ssi,hpg"]
            }),
            new FixedTimeProvider(new DateTimeOffset(2026, 7, 7, 3, 30, 0, TimeSpan.Zero)));

        var published = await publisher.PublishOnceAsync(CancellationToken.None);

        Assert.Equal(2, published);
        Assert.NotNull(provider.LastQuery);
        Assert.Equal("G1", provider.LastQuery.BoardId);
        Assert.Equal(["HPG", "SSI"], provider.LastQuery.Symbols);
        Assert.Equal(["HPG", "SSI"], broadcaster.Updates.Select(update => update.Symbol));
        Assert.All(broadcaster.Updates, update =>
        {
            Assert.Equal("G1", update.BoardId);
            Assert.NotNull(update.LastPrice);
            Assert.Equal(new DateTimeOffset(2026, 7, 7, 3, 30, 0, TimeSpan.Zero), update.UpdatedAt);
        });
        var status = Assert.Single(broadcaster.Statuses);
        Assert.True(status.IsEnabled);
        Assert.Equal("Mock", status.Provider);
    }

    [Fact]
    public async Task PublishOnceAsync_ByDefault_UsesMockSourceInsteadOfConfiguredMarketDataProvider()
    {
        var configuredProvider = new RecordingMarketDataProvider();
        var broadcaster = new RecordingQuoteBroadcaster();
        var publisher = new MockQuoteStreamPublisher(
            configuredProvider,
            new MockMarketDataProvider(),
            broadcaster,
            Options.Create(new MarketQuoteStreamOptions
            {
                BoardId = "g1",
                Symbols = ["ssi,hpg"]
            }),
            new FixedTimeProvider(new DateTimeOffset(2026, 7, 7, 3, 30, 0, TimeSpan.Zero)));

        var published = await publisher.PublishOnceAsync(CancellationToken.None);

        Assert.Equal(2, published);
        Assert.Null(configuredProvider.LastQuery);
        Assert.Equal(["HPG", "SSI"], broadcaster.Updates.Select(update => update.Symbol));
    }

    [Fact]
    public async Task PublishOnceAsync_GeneratesMockPricesAroundReferencePrice()
    {
        var provider = new RecordingMarketDataProvider(
        [
            RecordingMarketDataProvider.CreateQuote("SSI", 100m, 150m)
        ]);
        var broadcaster = new RecordingQuoteBroadcaster();
        var publisher = new MockQuoteStreamPublisher(
            provider,
            new MockMarketDataProvider(),
            broadcaster,
            Options.Create(new MarketQuoteStreamOptions
            {
                SourceProvider = MarketQuoteStreamOptions.ConfiguredSourceProvider,
                BoardId = "g1",
                Symbols = ["ssi"]
            }),
            new FixedTimeProvider(new DateTimeOffset(2026, 7, 7, 3, 30, 0, TimeSpan.Zero)));

        await publisher.PublishOnceAsync(CancellationToken.None);

        var update = Assert.Single(broadcaster.Updates);
        Assert.True(update.LastPrice < 100m);
        Assert.Equal(update.LastPrice - 100m, update.Change);
        Assert.Equal(Math.Round((update.LastPrice.Value - 100m) / 100m * 100m, 2, MidpointRounding.AwayFromZero), update.ChangePercent);
        Assert.True(update.Change < 0m);
        Assert.True(update.ChangePercent < 0m);
    }

    [Fact]
    public async Task PublishOnceAsync_GeneratesBidAndAskLevelUpdates()
    {
        var quote = RecordingMarketDataProvider.CreateQuote("SSI", 100m, 150m);
        var provider = new RecordingMarketDataProvider([quote]);
        var broadcaster = new RecordingQuoteBroadcaster();
        var publisher = new MockQuoteStreamPublisher(
            provider,
            new MockMarketDataProvider(),
            broadcaster,
            Options.Create(new MarketQuoteStreamOptions
            {
                SourceProvider = MarketQuoteStreamOptions.ConfiguredSourceProvider,
                BoardId = "g1",
                Symbols = ["ssi"]
            }),
            new FixedTimeProvider(new DateTimeOffset(2026, 7, 7, 3, 30, 0, TimeSpan.Zero)));

        await publisher.PublishOnceAsync(CancellationToken.None);

        var update = Assert.Single(broadcaster.Updates);
        Assert.NotNull(update.BidLevels);
        Assert.NotNull(update.AskLevels);
        var bid = Assert.Single(update.BidLevels);
        var ask = Assert.Single(update.AskLevels);
        Assert.NotEqual(quote.BidLevels[0].Price, bid.Price);
        Assert.NotEqual(quote.BidLevels[0].Quantity, bid.Quantity);
        Assert.NotEqual(quote.AskLevels[0].Price, ask.Price);
        Assert.NotEqual(quote.AskLevels[0].Quantity, ask.Quantity);
    }

    private sealed class RecordingMarketDataProvider : IMarketDataProvider
    {
        private readonly IReadOnlyList<MarketQuoteDto> _quotes;

        public RecordingMarketDataProvider()
            : this(
            [
                CreateQuote("HPG", 23.1m, 23.1m),
                CreateQuote("SSI", 26.75m, 26.7m)
            ])
        {
        }

        public RecordingMarketDataProvider(IReadOnlyList<MarketQuoteDto> quotes)
        {
            _quotes = quotes;
        }

        public MarketBoardQuery? LastQuery { get; private set; }

        public Task<IReadOnlyList<MarketQuoteDto>> GetMarketBoardAsync(
            MarketBoardQuery query,
            CancellationToken cancellationToken)
        {
            LastQuery = query;

            var symbolFilter = query.Symbols.ToHashSet(StringComparer.OrdinalIgnoreCase);
            var quotes = _quotes
                .Where(quote => symbolFilter.Count == 0 || symbolFilter.Contains(quote.Symbol))
                .OrderBy(quote => quote.Symbol, StringComparer.Ordinal)
                .ToArray();

            return Task.FromResult<IReadOnlyList<MarketQuoteDto>>(quotes);
        }

        public Task<SymbolDetailDto?> GetSymbolDetailAsync(
            string symbol,
            string boardId,
            CancellationToken cancellationToken)
        {
            return Task.FromResult<SymbolDetailDto?>(null);
        }

        public Task<IReadOnlyList<OhlcBarDto>> GetOhlcAsync(
            string symbol,
            string resolution,
            DateTimeOffset? from,
            DateTimeOffset? to,
            CancellationToken cancellationToken)
        {
            return Task.FromResult<IReadOnlyList<OhlcBarDto>>([]);
        }

        public Task<IReadOnlyList<MarketIndexDto>> GetMarketIndicesAsync(
            IReadOnlyCollection<string> indexNames,
            CancellationToken cancellationToken)
        {
            return Task.FromResult<IReadOnlyList<MarketIndexDto>>([]);
        }

        public Task<IReadOnlyList<OhlcBarDto>> GetIndexOhlcAsync(
            string indexName,
            string resolution,
            DateTimeOffset? from,
            DateTimeOffset? to,
            CancellationToken cancellationToken)
        {
            return Task.FromResult<IReadOnlyList<OhlcBarDto>>([]);
        }

        public Task<IReadOnlyList<MarketTradeDto>> GetLatestTradesAsync(
            string symbol,
            string boardId,
            int limit,
            CancellationToken cancellationToken)
        {
            return Task.FromResult<IReadOnlyList<MarketTradeDto>>([]);
        }

        public static MarketQuoteDto CreateQuote(string symbol, decimal referencePrice, decimal lastPrice)
        {
            var change = lastPrice - referencePrice;
            return new MarketQuoteDto(
                Symbol: symbol,
                BoardId: "G1",
                MarketId: "STO",
                DisplayName: symbol,
                ReferencePrice: referencePrice,
                CeilingPrice: referencePrice * 1.07m,
                FloorPrice: referencePrice * 0.93m,
                LastPrice: lastPrice,
                Change: change,
                ChangePercent: referencePrice > 0m ? change / referencePrice * 100m : 0m,
                LastQuantity: 100,
                TotalVolume: 1_000,
                TotalValue: lastPrice * 1_000,
                ForeignBuyVolume: 10,
                ForeignSellVolume: 20,
                ForeignRoom: 30,
                OpenPrice: lastPrice,
                HighPrice: lastPrice,
                LowPrice: lastPrice,
                BidLevels: [new PriceLevelDto(lastPrice, 100)],
                AskLevels: [new PriceLevelDto(lastPrice, 100)],
                TradingStatus: "NO_HALT",
                UpdatedAt: new DateTimeOffset(2026, 7, 7, 3, 29, 0, TimeSpan.Zero));
        }
    }

    private sealed class RecordingQuoteBroadcaster : IMarketQuoteBroadcaster
    {
        public List<MarketQuoteUpdateDto> Updates { get; } = [];

        public List<QuoteStreamStatusDto> Statuses { get; } = [];

        public Task BroadcastQuoteUpdateAsync(MarketQuoteUpdateDto update, CancellationToken cancellationToken)
        {
            Updates.Add(update);
            return Task.CompletedTask;
        }

        public Task BroadcastTradeUpdateAsync(MarketTradeUpdateDto update, CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }

        public Task BroadcastMarketIndexUpdateAsync(MarketIndexUpdateDto update, CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }

        public Task BroadcastStreamStatusAsync(QuoteStreamStatusDto status, CancellationToken cancellationToken)
        {
            Statuses.Add(status);
            return Task.CompletedTask;
        }
    }

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow()
        {
            return utcNow;
        }
    }
}
