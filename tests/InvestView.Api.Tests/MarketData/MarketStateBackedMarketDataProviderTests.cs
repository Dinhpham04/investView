using InvestView.Application.Abstractions.MarketData;
using InvestView.Application.Dtos.MarketData;
using InvestView.Infrastructure.MarketData;
using Microsoft.Extensions.Logging.Abstractions;

namespace InvestView.Api.Tests.MarketData;

public sealed class MarketStateBackedMarketDataProviderTests
{
    [Fact]
    public async Task GetMarketBoardAsync_WhenLocalMirrorHasPartialQuote_UsesSharedFullState()
    {
        var fallback = new EmptyMarketDataProvider();
        var localMirror = new InMemoryMarketStateStore();
        var sharedState = new InMemoryMarketStateStore();
        var provider = new MarketStateBackedMarketDataProvider(
            fallback,
            localMirror,
            sharedState,
            NullLogger<MarketStateBackedMarketDataProvider>.Instance);

        await sharedState.UpsertQuotesAsync(
        [
            CreateQuote("SSI", referencePrice: 26.7m, lastPrice: 27.1m)
        ], CancellationToken.None);
        await localMirror.ApplyQuoteUpdateAsync(
            new MarketQuoteUpdateDto(
                "SSI",
                "G1",
                LastPrice: 27.2m,
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
                UpdatedAt: new DateTimeOffset(2026, 7, 8, 3, 30, 0, TimeSpan.Zero)),
            CancellationToken.None);

        var quotes = await provider.GetMarketBoardAsync(
            new MarketBoardQuery(["SSI"], "G1", MarketId: null, IndexName: null),
            CancellationToken.None);

        var quote = Assert.Single(quotes);
        Assert.Equal(26.7m, quote.ReferencePrice);
        Assert.Equal(28.569m, quote.CeilingPrice);
        Assert.Equal(24.831m, quote.FloorPrice);
        Assert.Equal(27.1m, quote.LastPrice);
        Assert.Equal(0, fallback.MarketBoardCalls);
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

    private sealed class EmptyMarketDataProvider : IMarketDataProvider
    {
        public int MarketBoardCalls { get; private set; }

        public Task<IReadOnlyList<MarketQuoteDto>> GetMarketBoardAsync(
            MarketBoardQuery query,
            CancellationToken cancellationToken)
        {
            MarketBoardCalls++;
            return Task.FromResult<IReadOnlyList<MarketQuoteDto>>([]);
        }

        public Task<SymbolDetailDto?> GetSymbolDetailAsync(string symbol, string boardId, CancellationToken cancellationToken)
        {
            return Task.FromResult<SymbolDetailDto?>(null);
        }

        public Task<IReadOnlyList<MarketIndexDto>> GetMarketIndicesAsync(
            IReadOnlyCollection<string> indexNames,
            CancellationToken cancellationToken)
        {
            return Task.FromResult<IReadOnlyList<MarketIndexDto>>([]);
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
    }
}
