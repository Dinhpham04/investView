using InvestView.Application.Abstractions.MarketData;
using InvestView.Application.Dtos.MarketData;
using InvestView.Infrastructure.MarketData;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace InvestView.Api.Tests.MarketData;

public sealed class CachedMarketDataProviderTests
{
    [Fact]
    public async Task GetMarketBoardAsync_WhenCalledTwiceWithSameKey_UsesCachedQuotes()
    {
        using var memoryCache = new MemoryCache(new MemoryCacheOptions());
        var inner = new CountingMarketDataProvider();
        var provider = CreateProvider(inner, memoryCache, TimeSpan.FromMinutes(1));

        var first = await provider.GetMarketBoardAsync(new MarketBoardQuery(["SSI", "HPG"], "G1"), CancellationToken.None);
        var second = await provider.GetMarketBoardAsync(new MarketBoardQuery(["hpg", "ssi"], "g1"), CancellationToken.None);

        Assert.Equal(1, inner.MarketBoardCalls);
        Assert.Same(first, second);
    }

    [Fact]
    public async Task GetMarketBoardAsync_WhenSymbolsDiffer_UsesDifferentCacheEntries()
    {
        using var memoryCache = new MemoryCache(new MemoryCacheOptions());
        var inner = new CountingMarketDataProvider();
        var provider = CreateProvider(inner, memoryCache, TimeSpan.FromMinutes(1));

        await provider.GetMarketBoardAsync(new MarketBoardQuery(["HPG"], "G1"), CancellationToken.None);
        await provider.GetMarketBoardAsync(new MarketBoardQuery(["SSI"], "G1"), CancellationToken.None);

        Assert.Equal(2, inner.MarketBoardCalls);
    }

    [Fact]
    public async Task GetMarketBoardAsync_WhenBoardDiffers_UsesDifferentCacheEntries()
    {
        using var memoryCache = new MemoryCache(new MemoryCacheOptions());
        var inner = new CountingMarketDataProvider();
        var provider = CreateProvider(inner, memoryCache, TimeSpan.FromMinutes(1));

        await provider.GetMarketBoardAsync(new MarketBoardQuery(["HPG"], "G1"), CancellationToken.None);
        await provider.GetMarketBoardAsync(new MarketBoardQuery(["HPG"], "G2"), CancellationToken.None);

        Assert.Equal(2, inner.MarketBoardCalls);
    }

    [Fact]
    public async Task GetMarketBoardAsync_WhenMarketFilterDiffers_UsesDifferentCacheEntries()
    {
        using var memoryCache = new MemoryCache(new MemoryCacheOptions());
        var inner = new CountingMarketDataProvider();
        var provider = CreateProvider(inner, memoryCache, TimeSpan.FromMinutes(1));

        await provider.GetMarketBoardAsync(new MarketBoardQuery([], "G1", MarketId: "STO"), CancellationToken.None);
        await provider.GetMarketBoardAsync(new MarketBoardQuery([], "G1", IndexName: "VN30"), CancellationToken.None);

        Assert.Equal(2, inner.MarketBoardCalls);
    }

    [Fact]
    public async Task GetMarketBoardAsync_WhenTtlExpires_RefreshesInnerProvider()
    {
        using var memoryCache = new MemoryCache(new MemoryCacheOptions());
        var inner = new CountingMarketDataProvider();
        var provider = CreateProvider(inner, memoryCache, TimeSpan.FromMilliseconds(30));

        await provider.GetMarketBoardAsync(new MarketBoardQuery(["HPG"], "G1"), CancellationToken.None);
        await Task.Delay(80);
        await provider.GetMarketBoardAsync(new MarketBoardQuery(["HPG"], "G1"), CancellationToken.None);

        Assert.Equal(2, inner.MarketBoardCalls);
    }

    [Fact]
    public async Task GetSymbolDetailAsync_WhenCalledTwiceWithSameSymbol_UsesCachedDetail()
    {
        using var memoryCache = new MemoryCache(new MemoryCacheOptions());
        var inner = new CountingMarketDataProvider();
        var provider = CreateProvider(inner, memoryCache, TimeSpan.FromMinutes(1));

        var first = await provider.GetSymbolDetailAsync("hpg", "g1", CancellationToken.None);
        var second = await provider.GetSymbolDetailAsync("HPG", "G1", CancellationToken.None);

        Assert.Equal(1, inner.SymbolDetailCalls);
        Assert.Same(first, second);
    }

    [Fact]
    public async Task GetOhlcAsync_WhenCalledTwiceWithSameSymbolAndResolution_UsesCachedBars()
    {
        using var memoryCache = new MemoryCache(new MemoryCacheOptions());
        var inner = new CountingMarketDataProvider();
        var provider = CreateProvider(inner, memoryCache, TimeSpan.FromMinutes(1));

        var first = await provider.GetOhlcAsync("hpg", "1", null, null, CancellationToken.None);
        var second = await provider.GetOhlcAsync("HPG", "1", null, null, CancellationToken.None);

        Assert.Equal(1, inner.OhlcCalls);
        Assert.Same(first, second);
    }

    [Fact]
    public async Task GetLatestTradesAsync_WhenCalledTwiceWithSameKey_UsesCachedTrades()
    {
        using var memoryCache = new MemoryCache(new MemoryCacheOptions());
        var inner = new CountingMarketDataProvider();
        var provider = CreateProvider(inner, memoryCache, TimeSpan.FromMinutes(1));

        var first = await provider.GetLatestTradesAsync("hpg", "g1", 20, CancellationToken.None);
        var second = await provider.GetLatestTradesAsync("HPG", "G1", 20, CancellationToken.None);

        Assert.Equal(1, inner.LatestTradesCalls);
        Assert.Same(first, second);
    }

    private static CachedMarketDataProvider CreateProvider(
        IMarketDataProvider inner,
        IMemoryCache memoryCache,
        TimeSpan marketBoardTtl)
    {
        return new CachedMarketDataProvider(
            inner,
            memoryCache,
            Options.Create(new MarketDataCacheOptions
            {
                MarketBoardTtl = marketBoardTtl,
                SymbolDetailTtl = TimeSpan.FromMinutes(1),
                OhlcTtl = TimeSpan.FromMinutes(1),
                LatestTradesTtl = TimeSpan.FromMinutes(1)
            }),
            NullLogger<CachedMarketDataProvider>.Instance);
    }

    private sealed class CountingMarketDataProvider : IMarketDataProvider
    {
        public int MarketBoardCalls { get; private set; }

        public int SymbolDetailCalls { get; private set; }

        public int OhlcCalls { get; private set; }

        public int IndexOhlcCalls { get; private set; }

        public int MarketIndexCalls { get; private set; }

        public int LatestTradesCalls { get; private set; }

        public Task<IReadOnlyList<MarketQuoteDto>> GetMarketBoardAsync(
            MarketBoardQuery query,
            CancellationToken cancellationToken)
        {
            MarketBoardCalls++;

            IReadOnlyList<MarketQuoteDto> quotes =
            [
                new(
                    Symbol: string.Join(",", query.Symbols),
                    BoardId: query.BoardId,
                    MarketId: "MOCK",
                    DisplayName: "Mock quote",
                    ReferencePrice: 100m,
                    CeilingPrice: 107m,
                    FloorPrice: 93m,
                    LastPrice: 100m + MarketBoardCalls,
                    Change: MarketBoardCalls,
                    ChangePercent: MarketBoardCalls,
                    LastQuantity: 100,
                    TotalVolume: 1000,
                    TotalValue: 100000m,
                    ForeignBuyVolume: 100,
                    ForeignSellVolume: 90,
                    ForeignRoom: 10000,
                    OpenPrice: 100m,
                    HighPrice: 101m,
                    LowPrice: 99m,
                    BidLevels: [new PriceLevelDto(100m, 1000)],
                    AskLevels: [new PriceLevelDto(101m, 1000)],
                    TradingStatus: "Continuous",
                    UpdatedAt: new DateTimeOffset(2026, 7, 5, 7, 45, 0, TimeSpan.Zero))
            ];

            return Task.FromResult(quotes);
        }

        public Task<SymbolDetailDto?> GetSymbolDetailAsync(
            string symbol,
            string boardId,
            CancellationToken cancellationToken)
        {
            SymbolDetailCalls++;

            return Task.FromResult<SymbolDetailDto?>(new SymbolDetailDto(
                symbol,
                boardId,
                "MOCK",
                "Mock symbol",
                "Mock symbol",
                "Stock",
                "VN000000MOCK",
                "STOCK",
                "ST",
                100m,
                107m,
                93m,
                100m,
                0m,
                0m,
                100,
                1000,
                100000m,
                100,
                90,
                10000,
                100m,
                101m,
                99m,
                [new PriceLevelDto(100m, 1000)],
                [new PriceLevelDto(101m, 1000)],
                "Continuous",
                "NORMAL",
                "NORMAL",
                "NORMAL",
                new DateTimeOffset(2026, 7, 5, 0, 0, 0, TimeSpan.Zero),
                null,
                0,
                new DateTimeOffset(2026, 7, 5, 7, 45, 0, TimeSpan.Zero)));
        }

        public Task<IReadOnlyList<OhlcBarDto>> GetOhlcAsync(
            string symbol,
            string resolution,
            DateTimeOffset? from,
            DateTimeOffset? to,
            CancellationToken cancellationToken)
        {
            OhlcCalls++;

            IReadOnlyList<OhlcBarDto> bars =
            [
                new(
                    symbol,
                    resolution,
                    new DateTimeOffset(2026, 7, 5, 7, 45, 0, TimeSpan.Zero),
                    100m,
                    101m,
                    99m,
                    100m,
                    1000)
            ];

            return Task.FromResult(bars);
        }

        public Task<IReadOnlyList<MarketIndexDto>> GetMarketIndicesAsync(
            IReadOnlyCollection<string> indexNames,
            CancellationToken cancellationToken)
        {
            MarketIndexCalls++;

            IReadOnlyList<MarketIndexDto> indices =
            [
                new(
                    IndexName: indexNames.FirstOrDefault() ?? "VNINDEX",
                    Value: 1000m + MarketIndexCalls,
                    Change: MarketIndexCalls,
                    ChangePercent: MarketIndexCalls,
                    ReferenceValue: 1000m,
                    HighValue: 1002m,
                    LowValue: 999m,
                    TotalVolume: 1000,
                    TotalValue: 100000m,
                    UpCount: 10,
                    DownCount: 5,
                    NoChangeCount: 3,
                    CeilingCount: 1,
                    FloorCount: 0,
                    MarketId: "STO",
                    TradingSessionId: "Continuous",
                    UpdatedAt: new DateTimeOffset(2026, 7, 5, 7, 45, 0, TimeSpan.Zero))
            ];

            return Task.FromResult(indices);
        }

        public Task<IReadOnlyList<OhlcBarDto>> GetIndexOhlcAsync(
            string indexName,
            string resolution,
            DateTimeOffset? from,
            DateTimeOffset? to,
            CancellationToken cancellationToken)
        {
            IndexOhlcCalls++;

            IReadOnlyList<OhlcBarDto> bars =
            [
                new(indexName, resolution, new DateTimeOffset(2026, 7, 5, 7, 45, 0, TimeSpan.Zero), 1000m, 1001m, 999m, 1000m, 1000)
            ];

            return Task.FromResult(bars);
        }

        public Task<IReadOnlyList<MarketTradeDto>> GetLatestTradesAsync(
            string symbol,
            string boardId,
            int limit,
            CancellationToken cancellationToken)
        {
            LatestTradesCalls++;

            IReadOnlyList<MarketTradeDto> trades =
            [
                new(
                    symbol,
                    boardId,
                    new DateTimeOffset(2026, 7, 5, 7, 45, 0, TimeSpan.Zero),
                    100m,
                    0m,
                    0m,
                    100,
                    1000,
                    100000m,
                    string.Empty)
            ];

            return Task.FromResult(trades);
        }
    }
}
