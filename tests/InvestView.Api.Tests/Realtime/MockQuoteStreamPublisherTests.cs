using InvestView.Application.Abstractions.MarketData;
using InvestView.Application.Abstractions.Realtime;
using InvestView.Application.Dtos.MarketData;
using InvestView.Application.Dtos.Realtime;
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
            broadcaster,
            Options.Create(new MarketQuoteStreamOptions
            {
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

    private sealed class RecordingMarketDataProvider : IMarketDataProvider
    {
        public MarketBoardQuery? LastQuery { get; private set; }

        public Task<IReadOnlyList<MarketQuoteDto>> GetMarketBoardAsync(
            MarketBoardQuery query,
            CancellationToken cancellationToken)
        {
            LastQuery = query;
            IReadOnlyList<MarketQuoteDto> quotes =
            [
                CreateQuote("HPG", 23.1m, 23.1m),
                CreateQuote("SSI", 26.75m, 26.7m)
            ];

            return Task.FromResult(quotes);
        }

        public Task<SymbolDetailDto?> GetSymbolDetailAsync(string symbol, CancellationToken cancellationToken)
        {
            return Task.FromResult<SymbolDetailDto?>(null);
        }

        public Task<IReadOnlyList<OhlcBarDto>> GetOhlcAsync(
            string symbol,
            string resolution,
            CancellationToken cancellationToken)
        {
            return Task.FromResult<IReadOnlyList<OhlcBarDto>>([]);
        }

        private static MarketQuoteDto CreateQuote(string symbol, decimal referencePrice, decimal lastPrice)
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
