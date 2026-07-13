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

    [Fact]
    public async Task GetMarketBoardAsync_WhenQueryHasNoSymbolsAndSharedMembershipHasQuotes_DoesNotCallFallback()
    {
        var fallback = new EmptyMarketDataProvider();
        var localMirror = new InMemoryMarketStateStore();
        var sharedState = new InMemoryMarketStateStore();
        var provider = new MarketStateBackedMarketDataProvider(
            fallback,
            localMirror,
            sharedState,
            NullLogger<MarketStateBackedMarketDataProvider>.Instance);
        var query = new MarketBoardQuery([], "G1", MarketId: "STO", IndexName: "VN30");
        await sharedState.UpsertQuotesAsync(
        [
            CreateQuote("SSI", referencePrice: 26.7m, lastPrice: 27.1m),
            CreateQuote("HPG", referencePrice: 23.5m, lastPrice: 24.0m)
        ], CancellationToken.None);
        await sharedState.UpsertSymbolMembershipsAsync(query, ["SSI", "HPG"], CancellationToken.None);

        var quotes = await provider.GetMarketBoardAsync(query, CancellationToken.None);

        Assert.Equal(["HPG", "SSI"], quotes.Select(quote => quote.Symbol).ToArray());
        Assert.Equal(0, fallback.MarketBoardCalls);
        var localMembership = await localMirror.GetSymbolMembershipsAsync(query, CancellationToken.None);
        Assert.Equal(["HPG", "SSI"], localMembership.Order(StringComparer.Ordinal).ToArray());
    }

    [Fact]
    public async Task GetMarketBoardAsync_WhenSharedStateHasForeignTradingFields_ReturnsCachedFieldsWithoutFallback()
    {
        var fallback = new EmptyMarketDataProvider();
        var localMirror = new InMemoryMarketStateStore();
        var sharedState = new InMemoryMarketStateStore();
        var provider = new MarketStateBackedMarketDataProvider(
            fallback,
            localMirror,
            sharedState,
            NullLogger<MarketStateBackedMarketDataProvider>.Instance);
        var query = new MarketBoardQuery(["SSI"], "G1", MarketId: null, IndexName: null);
        await sharedState.UpsertQuotesAsync(
        [
            CreateQuote("SSI", referencePrice: 26.7m, lastPrice: 27.1m)
        ], CancellationToken.None);

        var quotes = await provider.GetMarketBoardAsync(query, CancellationToken.None);

        var quote = Assert.Single(quotes);
        Assert.Equal(10, quote.ForeignBuyVolume);
        Assert.Equal(20, quote.ForeignSellVolume);
        Assert.Equal(100, quote.ForeignRoom);
        Assert.Equal(0, fallback.MarketBoardCalls);
    }

    [Fact]
    public async Task GetMarketBoardAsync_WhenMembershipHasPartialState_BackfillsOnlyMissingSymbols()
    {
        var fallback = new EmptyMarketDataProvider
        {
            MarketBoardQuotes =
            [
                CreateQuote("HPG", referencePrice: 23.5m, lastPrice: 24.0m)
            ]
        };
        var localMirror = new InMemoryMarketStateStore();
        var sharedState = new InMemoryMarketStateStore();
        var provider = new MarketStateBackedMarketDataProvider(
            fallback,
            localMirror,
            sharedState,
            NullLogger<MarketStateBackedMarketDataProvider>.Instance);
        var query = new MarketBoardQuery([], "G1", MarketId: "STO", IndexName: "VN30");
        await sharedState.UpsertQuotesAsync(
        [
            CreateQuote("SSI", referencePrice: 26.7m, lastPrice: 27.1m)
        ], CancellationToken.None);
        await sharedState.UpsertSymbolMembershipsAsync(query, ["SSI", "HPG"], CancellationToken.None);

        var quotes = await provider.GetMarketBoardAsync(query, CancellationToken.None);

        Assert.Equal(["HPG", "SSI"], quotes.Select(quote => quote.Symbol).ToArray());
        Assert.Equal(1, fallback.MarketBoardCalls);
        Assert.Equal(["HPG"], fallback.LastMarketBoardSymbols);
    }

    [Fact]
    public async Task GetMarketBoardAsync_WhenCategoryCacheExists_DoesNotTreatItAsCompleteMarketMembership()
    {
        var fallback = new EmptyMarketDataProvider
        {
            MarketBoardQuotes =
            [
                CreateQuote("AAM", referencePrice: 10m, lastPrice: 10.2m),
                CreateQuote("HPG", referencePrice: 23.5m, lastPrice: 24.0m),
                CreateQuote("SSI", referencePrice: 26.7m, lastPrice: 27.1m),
                CreateQuote("VCB", referencePrice: 58m, lastPrice: 58.5m)
            ]
        };
        var localMirror = new InMemoryMarketStateStore();
        var sharedState = new InMemoryMarketStateStore();
        var provider = new MarketStateBackedMarketDataProvider(
            fallback,
            localMirror,
            sharedState,
            NullLogger<MarketStateBackedMarketDataProvider>.Instance);

        var vn30Query = new MarketBoardQuery([], "G1", MarketId: "STO", IndexName: "VN30");
        await sharedState.UpsertQuotesAsync(
        [
            CreateQuote("HPG", referencePrice: 23.5m, lastPrice: 24.0m),
            CreateQuote("SSI", referencePrice: 26.7m, lastPrice: 27.1m),
            CreateQuote("VCB", referencePrice: 58m, lastPrice: 58.5m)
        ], CancellationToken.None);
        await sharedState.UpsertSymbolMembershipsAsync(vn30Query, ["HPG", "SSI", "VCB"], CancellationToken.None);

        var hoseQuery = new MarketBoardQuery([], "G1", MarketId: "STO", IndexName: null);
        var quotes = await provider.GetMarketBoardAsync(hoseQuery, CancellationToken.None);

        Assert.Equal(["AAM", "HPG", "SSI", "VCB"], quotes.Select(quote => quote.Symbol).ToArray());
        Assert.Equal(1, fallback.MarketBoardCalls);
        Assert.Empty(fallback.LastMarketBoardSymbols);
    }

    [Fact]
    public async Task GetSymbolDetailAsync_WhenSharedStateHasDetail_UsesSharedStateAndWarmsLocalMirror()
    {
        var fallback = new EmptyMarketDataProvider();
        var localMirror = new InMemoryMarketStateStore();
        var sharedState = new InMemoryMarketStateStore();
        var provider = new MarketStateBackedMarketDataProvider(
            fallback,
            localMirror,
            sharedState,
            NullLogger<MarketStateBackedMarketDataProvider>.Instance);
        var detail = CreateDetail("SSI", referencePrice: 26.7m, lastPrice: 27.1m);
        await sharedState.UpsertSymbolDetailAsync(detail, CancellationToken.None);

        var result = await provider.GetSymbolDetailAsync("ssi", "g1", CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal("SSI", result.Symbol);
        Assert.Equal(0, fallback.SymbolDetailCalls);
        var localQuote = Assert.Single(await localMirror.GetQuotesAsync("G1", ["SSI"], CancellationToken.None));
        Assert.Equal(27.1m, localQuote.LastPrice);
    }

    [Fact]
    public async Task GetSymbolDetailAsync_WhenSharedStateHasUsableQuote_BuildsSnapshotDetailWithoutFallback()
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

        var result = await provider.GetSymbolDetailAsync("ssi", "g1", CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal("SSI", result.Symbol);
        Assert.Equal("SSI", result.DisplayName);
        Assert.Equal(26.7m, result.ReferencePrice);
        Assert.Equal(28.569m, result.CeilingPrice);
        Assert.Equal(24.831m, result.FloorPrice);
        Assert.Equal(27.1m, result.LastPrice);
        Assert.Equal(10, result.ForeignBuyVolume);
        Assert.Equal(20, result.ForeignSellVolume);
        Assert.Equal(100, result.ForeignRoom);
        Assert.Equal(string.Empty, result.Isin);
        Assert.Equal(0, fallback.SymbolDetailCalls);

        var localQuote = Assert.Single(await localMirror.GetQuotesAsync("G1", ["SSI"], CancellationToken.None));
        Assert.Equal(27.1m, localQuote.LastPrice);
    }

    [Fact]
    public async Task GetSymbolDetailAsync_WhenSharedStateHasUsableQuote_BackfillsMetadataWithoutPriceFallback()
    {
        var fallback = new EmptyMarketDataProvider
        {
            SymbolMetadata = CreateMetadata("SSI")
        };
        var localMirror = new InMemoryMarketStateStore();
        var sharedState = new InMemoryMarketStateStore();
        var provider = new MarketStateBackedMarketDataProvider(
            fallback,
            localMirror,
            sharedState,
            NullLogger<MarketStateBackedMarketDataProvider>.Instance,
            fallback);
        await sharedState.UpsertQuotesAsync(
        [
            CreateQuote("SSI", referencePrice: 26.7m, lastPrice: 27.1m)
        ], CancellationToken.None);

        var result = await provider.GetSymbolDetailAsync("ssi", "g1", CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal("SSI Corporation", result.Name);
        Assert.Equal("VN000000SSI", result.Isin);
        Assert.Equal("STOCK", result.ProductGroupId);
        Assert.Equal("ST", result.SecurityGroupId);
        Assert.Equal(26.7m, result.ReferencePrice);
        Assert.Equal(27.1m, result.LastPrice);
        Assert.Equal(10, result.ForeignBuyVolume);
        Assert.Equal(20, result.ForeignSellVolume);
        Assert.Equal(0, fallback.SymbolDetailCalls);
        Assert.Equal(1, fallback.SymbolMetadataCalls);

        var cachedDetail = await sharedState.GetSymbolDetailAsync("SSI", "G1", CancellationToken.None);
        Assert.NotNull(cachedDetail);
        Assert.Equal("SSI Corporation", cachedDetail.Name);
        Assert.Equal(27.1m, cachedDetail.LastPrice);
    }

    [Fact]
    public async Task GetOhlcAsync_WhenSharedStateHasCoveredRange_UsesSharedStateAndWarmsLocalMirror()
    {
        var fallback = new EmptyMarketDataProvider();
        var localMirror = new InMemoryMarketStateStore();
        var sharedState = new InMemoryMarketStateStore();
        var provider = new MarketStateBackedMarketDataProvider(
            fallback,
            localMirror,
            sharedState,
            NullLogger<MarketStateBackedMarketDataProvider>.Instance);
        var from = new DateTimeOffset(2026, 7, 8, 2, 0, 0, TimeSpan.Zero);
        var to = from.AddHours(1);
        var bars = CreateBars("SSI", "1", from);
        await sharedState.UpsertOhlcBarsAsync("ssi", "1", from, to, bars, CancellationToken.None);

        var result = await provider.GetOhlcAsync("ssi", "1", from, to, CancellationToken.None);

        Assert.Equal(2, result.Count);
        Assert.Equal(0, fallback.OhlcCalls);
        var localBars = await localMirror.GetOhlcBarsAsync("SSI", "1", from, to, CancellationToken.None);
        Assert.Equal(2, localBars.Count);
    }

    [Fact]
    public async Task GetOhlcAsync_WhenStateMissing_BackfillsSharedAndLocalFromFallback()
    {
        var from = new DateTimeOffset(2026, 7, 8, 2, 0, 0, TimeSpan.Zero);
        var to = from.AddHours(1);
        var fallback = new EmptyMarketDataProvider
        {
            OhlcBars = CreateBars("SSI", "1", from)
        };
        var localMirror = new InMemoryMarketStateStore();
        var sharedState = new InMemoryMarketStateStore();
        var provider = new MarketStateBackedMarketDataProvider(
            fallback,
            localMirror,
            sharedState,
            NullLogger<MarketStateBackedMarketDataProvider>.Instance);

        var result = await provider.GetOhlcAsync("ssi", "1", from, to, CancellationToken.None);

        Assert.Equal(2, result.Count);
        Assert.Equal(1, fallback.OhlcCalls);
        var sharedBars = await sharedState.GetOhlcBarsAsync("SSI", "1", from, to, CancellationToken.None);
        var localBars = await localMirror.GetOhlcBarsAsync("SSI", "1", from, to, CancellationToken.None);
        Assert.Equal(2, sharedBars.Count);
        Assert.Equal(2, localBars.Count);
    }

    [Fact]
    public async Task GetOhlcAsync_WhenFallbackReturnsEmpty_MarksCoverageAndDoesNotBackfillSameRangeAgain()
    {
        var from = new DateTimeOffset(2026, 7, 8, 2, 0, 0, TimeSpan.Zero);
        var to = from.AddHours(1);
        var fallback = new EmptyMarketDataProvider();
        var localMirror = new InMemoryMarketStateStore();
        var sharedState = new InMemoryMarketStateStore();
        var provider = new MarketStateBackedMarketDataProvider(
            fallback,
            localMirror,
            sharedState,
            NullLogger<MarketStateBackedMarketDataProvider>.Instance);

        var firstResult = await provider.GetOhlcAsync("ssi", "1", from, to, CancellationToken.None);
        var secondResult = await provider.GetOhlcAsync("ssi", "1", from, to, CancellationToken.None);

        Assert.Empty(firstResult);
        Assert.Empty(secondResult);
        Assert.Equal(1, fallback.OhlcCalls);
    }

    [Fact]
    public async Task GetIndexOhlcAsync_WhenLocalMirrorHasRealtimePartialRange_BackfillsFromFallback()
    {
        var from = new DateTimeOffset(2026, 7, 8, 2, 0, 0, TimeSpan.Zero);
        var to = from.AddHours(6);
        var fallback = new EmptyMarketDataProvider
        {
            IndexOhlcBars = CreateBars("VNINDEX", "1", from)
        };
        var localMirror = new InMemoryMarketStateStore();
        var sharedState = new InMemoryMarketStateStore();
        var provider = new MarketStateBackedMarketDataProvider(
            fallback,
            localMirror,
            sharedState,
            NullLogger<MarketStateBackedMarketDataProvider>.Instance);
        await localMirror.ApplyOhlcUpdateAsync(
            new MarketOhlcUpdateDto(
                "vnindex",
                "1",
                from.AddHours(3),
                1840m,
                1842m,
                1838m,
                1841m,
                10_000,
                "index",
                false,
                from.AddHours(3)),
            CancellationToken.None);

        var result = await provider.GetIndexOhlcAsync("vnindex", "1", from, to, CancellationToken.None);

        Assert.Equal(2, result.Count);
        Assert.Equal(1, fallback.IndexOhlcCalls);
        var sharedBars = await sharedState.GetIndexOhlcBarsAsync("VNINDEX", "1", from, to, CancellationToken.None);
        var localBars = await localMirror.GetIndexOhlcBarsAsync("VNINDEX", "1", from, to, CancellationToken.None);
        Assert.Equal(2, sharedBars.Count);
        Assert.Equal(2, localBars.Count);
    }

    [Fact]
    public async Task GetIndexOhlcAsync_WhenFallbackReturnsEmpty_MarksCoverageAndDoesNotBackfillSameRangeAgain()
    {
        var from = new DateTimeOffset(2026, 7, 8, 2, 0, 0, TimeSpan.Zero);
        var to = from.AddHours(1);
        var fallback = new EmptyMarketDataProvider();
        var localMirror = new InMemoryMarketStateStore();
        var sharedState = new InMemoryMarketStateStore();
        var provider = new MarketStateBackedMarketDataProvider(
            fallback,
            localMirror,
            sharedState,
            NullLogger<MarketStateBackedMarketDataProvider>.Instance);

        var firstResult = await provider.GetIndexOhlcAsync("vnindex", "1", from, to, CancellationToken.None);
        var secondResult = await provider.GetIndexOhlcAsync("vnindex", "1", from, to, CancellationToken.None);

        Assert.Empty(firstResult);
        Assert.Empty(secondResult);
        Assert.Equal(1, fallback.IndexOhlcCalls);
    }

    [Fact]
    public async Task GetIndexOhlcAsync_WhenRealtimeExtendsCachedRange_DoesNotBackfillNextMinute()
    {
        var from = new DateTimeOffset(2026, 7, 8, 2, 0, 0, TimeSpan.Zero);
        var firstTo = from.AddMinutes(30);
        var nextTo = firstTo.AddMinutes(1);
        var fallback = new EmptyMarketDataProvider
        {
            IndexOhlcBars = CreateBars("VNINDEX", "1", from)
        };
        var localMirror = new InMemoryMarketStateStore();
        var sharedState = new InMemoryMarketStateStore();
        var provider = new MarketStateBackedMarketDataProvider(
            fallback,
            localMirror,
            sharedState,
            NullLogger<MarketStateBackedMarketDataProvider>.Instance);

        var firstResult = await provider.GetIndexOhlcAsync("vnindex", "1", from, firstTo, CancellationToken.None);
        await sharedState.ApplyOhlcUpdateAsync(
            new MarketOhlcUpdateDto(
                "vnindex",
                "1",
                nextTo,
                1840m,
                1843m,
                1839m,
                1842m,
                14_000,
                "index",
                false,
                nextTo),
            CancellationToken.None);

        var secondResult = await provider.GetIndexOhlcAsync("vnindex", "1", from, nextTo, CancellationToken.None);

        Assert.Equal(2, firstResult.Count);
        Assert.Equal(1, fallback.IndexOhlcCalls);
        Assert.Equal(3, secondResult.Count);
        Assert.Contains(secondResult, bar => bar.Time == nextTo && bar.Close == 1842m);
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

    private static SymbolDetailDto CreateDetail(string symbol, decimal referencePrice, decimal lastPrice)
    {
        var change = lastPrice - referencePrice;
        return new SymbolDetailDto(
            symbol,
            "G1",
            "STO",
            symbol,
            $"{symbol} Corporation",
            "Stock",
            $"VN000000{symbol}",
            "STOCK",
            "ST",
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
            "NORMAL",
            "NORMAL",
            "NORMAL",
            new DateTimeOffset(2020, 1, 1, 0, 0, 0, TimeSpan.Zero),
            null,
            0,
            new DateTimeOffset(2026, 7, 8, 3, 29, 0, TimeSpan.Zero));
    }

    private static SymbolMetadataDto CreateMetadata(string symbol)
    {
        return new SymbolMetadataDto(
            symbol,
            "G1",
            "STO",
            $"{symbol} Securities",
            $"{symbol} Corporation",
            "Stock",
            $"VN000000{symbol}",
            "STOCK",
            "ST",
            "Continuous",
            "NORMAL",
            "NORMAL",
            "NORMAL",
            new DateTimeOffset(2020, 1, 1, 0, 0, 0, TimeSpan.Zero),
            null,
            0,
            new DateTimeOffset(2026, 7, 8, 3, 0, 0, TimeSpan.Zero));
    }

    private static IReadOnlyList<OhlcBarDto> CreateBars(string symbol, string resolution, DateTimeOffset from)
    {
        return
        [
            new(symbol, resolution, from.AddMinutes(1), 26.7m, 27.1m, 26.6m, 27m, 10_000),
            new(symbol, resolution, from.AddMinutes(2), 27m, 27.2m, 26.9m, 27.1m, 12_000)
        ];
    }

    private sealed class EmptyMarketDataProvider : IMarketDataProvider, ISymbolMetadataProvider
    {
        public int MarketBoardCalls { get; private set; }

        public IReadOnlyList<string> LastMarketBoardSymbols { get; private set; } = [];

        public IReadOnlyList<MarketQuoteDto> MarketBoardQuotes { get; init; } = [];

        public int SymbolDetailCalls { get; private set; }

        public int SymbolMetadataCalls { get; private set; }

        public SymbolMetadataDto? SymbolMetadata { get; init; }

        public int OhlcCalls { get; private set; }

        public IReadOnlyList<OhlcBarDto> OhlcBars { get; init; } = [];

        public int IndexOhlcCalls { get; private set; }

        public IReadOnlyList<OhlcBarDto> IndexOhlcBars { get; init; } = [];

        public Task<IReadOnlyList<MarketQuoteDto>> GetMarketBoardAsync(
            MarketBoardQuery query,
            CancellationToken cancellationToken)
        {
            MarketBoardCalls++;
            LastMarketBoardSymbols = query.Symbols.ToArray();
            var requestedSymbols = query.Symbols.ToHashSet(StringComparer.OrdinalIgnoreCase);
            var quotes = requestedSymbols.Count == 0
                ? MarketBoardQuotes
                : MarketBoardQuotes
                    .Where(quote => requestedSymbols.Contains(quote.Symbol))
                    .ToArray();
            return Task.FromResult<IReadOnlyList<MarketQuoteDto>>(quotes);
        }

        public Task<SymbolDetailDto?> GetSymbolDetailAsync(string symbol, string boardId, CancellationToken cancellationToken)
        {
            SymbolDetailCalls++;
            return Task.FromResult<SymbolDetailDto?>(null);
        }

        public Task<SymbolMetadataDto?> GetSymbolMetadataAsync(
            string symbol,
            string boardId,
            CancellationToken cancellationToken)
        {
            SymbolMetadataCalls++;
            return Task.FromResult(SymbolMetadata);
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
            OhlcCalls++;
            return Task.FromResult(OhlcBars);
        }

        public Task<IReadOnlyList<OhlcBarDto>> GetIndexOhlcAsync(
            string indexName,
            string resolution,
            DateTimeOffset? from,
            DateTimeOffset? to,
            CancellationToken cancellationToken)
        {
            IndexOhlcCalls++;
            return Task.FromResult(IndexOhlcBars);
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
