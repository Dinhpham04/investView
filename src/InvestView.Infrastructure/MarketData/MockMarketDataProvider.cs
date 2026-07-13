using InvestView.Application.Abstractions.MarketData;
using InvestView.Application.Dtos.MarketData;

namespace InvestView.Infrastructure.MarketData;

public sealed class MockMarketDataProvider : IMarketDataProvider, ISymbolMetadataProvider
{
    public const string DefaultBoardId = "G1";

    private static readonly DateTimeOffset SnapshotTime =
        new(2026, 7, 3, 7, 45, 0, TimeSpan.Zero);

    private static readonly IReadOnlyList<MarketQuoteDto> Quotes =
    [
        new(
            Symbol: "HPG",
            BoardId: DefaultBoardId,
            MarketId: "HOSE",
            DisplayName: "Hoa Phat Group",
            ReferencePrice: 28600m,
            CeilingPrice: 30600m,
            FloorPrice: 26600m,
            LastPrice: 29150m,
            Change: 550m,
            ChangePercent: 1.92m,
            LastQuantity: 2500,
            TotalVolume: 12_450_000,
            TotalValue: 362_917_500_000m,
            ForeignBuyVolume: 786_100,
            ForeignSellVolume: 1_227_649,
            ForeignRoom: 1_742_502_798,
            OpenPrice: 28700m,
            HighPrice: 29200m,
            LowPrice: 28450m,
            BidLevels:
            [
                new PriceLevelDto(29100m, 18300),
                new PriceLevelDto(29050m, 22500),
                new PriceLevelDto(29000m, 41300)
            ],
            AskLevels:
            [
                new PriceLevelDto(29150m, 12000),
                new PriceLevelDto(29200m, 17600),
                new PriceLevelDto(29250m, 28400)
            ],
            TradingStatus: "Continuous",
            UpdatedAt: SnapshotTime),
        new(
            Symbol: "SSI",
            BoardId: DefaultBoardId,
            MarketId: "HOSE",
            DisplayName: "SSI Securities",
            ReferencePrice: 35200m,
            CeilingPrice: 37650m,
            FloorPrice: 32750m,
            LastPrice: 34850m,
            Change: -350m,
            ChangePercent: -0.99m,
            LastQuantity: 1800,
            TotalVolume: 7_820_000,
            TotalValue: 272_527_000_000m,
            ForeignBuyVolume: 2_410_791,
            ForeignSellVolume: 1_038_440,
            ForeignRoom: 360_456_325,
            OpenPrice: 35400m,
            HighPrice: 35600m,
            LowPrice: 34700m,
            BidLevels:
            [
                new PriceLevelDto(34800m, 15400),
                new PriceLevelDto(34750m, 28100),
                new PriceLevelDto(34700m, 35000)
            ],
            AskLevels:
            [
                new PriceLevelDto(34850m, 9400),
                new PriceLevelDto(34900m, 16300),
                new PriceLevelDto(34950m, 20700)
            ],
            TradingStatus: "Continuous",
            UpdatedAt: SnapshotTime),
        new(
            Symbol: "VCB",
            BoardId: DefaultBoardId,
            MarketId: "HOSE",
            DisplayName: "Vietcombank",
            ReferencePrice: 62400m,
            CeilingPrice: 66700m,
            FloorPrice: 58100m,
            LastPrice: 62400m,
            Change: 0m,
            ChangePercent: 0m,
            LastQuantity: 900,
            TotalVolume: 2_140_000,
            TotalValue: 133_536_000_000m,
            ForeignBuyVolume: 2_419_600,
            ForeignSellVolume: 1_736_409,
            ForeignRoom: 278_589_387,
            OpenPrice: 62300m,
            HighPrice: 62700m,
            LowPrice: 62100m,
            BidLevels:
            [
                new PriceLevelDto(62300m, 7200),
                new PriceLevelDto(62200m, 10600),
                new PriceLevelDto(62100m, 15500)
            ],
            AskLevels:
            [
                new PriceLevelDto(62400m, 6300),
                new PriceLevelDto(62500m, 8900),
                new PriceLevelDto(62600m, 13100)
            ],
            TradingStatus: "Continuous",
            UpdatedAt: SnapshotTime)
    ];

    private static readonly IReadOnlyList<MarketIndexDto> Indices =
    [
        new("VNINDEX", 1840.70m, -13.00m, -0.70m, 1853.70m, 1857.00m, 1831.25m, 585_707_000, 14_603.675m, 92, 206, 66, 1, 3, "STO", "Continuous", SnapshotTime),
        new("VN30", 1987.11m, -11.33m, -0.57m, 1998.44m, 2001.12m, 1977.64m, 226_301_000, 7_232.9m, 7, 19, 4, 0, 0, "STO", "Continuous", SnapshotTime),
        new("HNX30", 514.73m, -2.25m, -0.44m, 516.98m, 518.41m, 511.82m, 42_094_000, 933.109m, 9, 14, 7, 0, 0, "STX", "Continuous", SnapshotTime),
        new("HNX", 306.67m, 6.28m, 2.09m, 300.39m, 307.42m, 299.96m, 56_859_000, 1_096.426m, 47, 76, 50, 2, 3, "STX", "PLO", SnapshotTime),
        new("UPCOM", 128.68m, 0.67m, 0.52m, 128.01m, 129.04m, 127.85m, 23_428_000, 281.722m, 85, 101, 79, 0, 1, "UPX", "Continuous", SnapshotTime)
    ];

    public Task<IReadOnlyList<MarketQuoteDto>> GetMarketBoardAsync(
        MarketBoardQuery query,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var normalizedBoardId = NormalizeBoardId(query.BoardId);
        var symbolFilter = NormalizeSymbols(query.Symbols);
        var exchangeFilter = NormalizeExchange(query.MarketId);
        var indexFilter = NormalizeToken(query.IndexName);

        var quotes = Quotes
            .Where(quote => quote.BoardId.Equals(normalizedBoardId, StringComparison.OrdinalIgnoreCase))
            .Where(quote => symbolFilter.Count == 0 || symbolFilter.Contains(quote.Symbol))
            .Where(quote => string.IsNullOrWhiteSpace(exchangeFilter) || quote.MarketId.Equals(exchangeFilter, StringComparison.OrdinalIgnoreCase))
            .Where(quote => string.IsNullOrWhiteSpace(indexFilter) || IsMockIndexMember(indexFilter, quote.Symbol))
            .OrderBy(quote => quote.Symbol, StringComparer.Ordinal)
            .ToArray();

        return Task.FromResult<IReadOnlyList<MarketQuoteDto>>(quotes);
    }

    public Task<SymbolDetailDto?> GetSymbolDetailAsync(
        string symbol,
        string boardId,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var normalizedBoardId = NormalizeBoardId(boardId);
        var quote = Quotes.FirstOrDefault(item =>
            item.Symbol.Equals(symbol, StringComparison.OrdinalIgnoreCase) &&
            item.BoardId.Equals(normalizedBoardId, StringComparison.OrdinalIgnoreCase));
        if (quote is null)
        {
            return Task.FromResult<SymbolDetailDto?>(null);
        }

        var detail = new SymbolDetailDto(
            quote.Symbol,
            quote.BoardId,
            quote.MarketId,
            quote.DisplayName,
            quote.DisplayName,
            "Stock",
            "VN000000" + quote.Symbol,
            "STOCK",
            "ST",
            quote.ReferencePrice,
            quote.CeilingPrice,
            quote.FloorPrice,
            quote.LastPrice,
            quote.Change,
            quote.ChangePercent,
            quote.LastQuantity,
            quote.TotalVolume,
            quote.TotalValue,
            quote.ForeignBuyVolume,
            quote.ForeignSellVolume,
            quote.ForeignRoom,
            quote.OpenPrice,
            quote.HighPrice,
            quote.LowPrice,
            quote.BidLevels,
            quote.AskLevels,
            quote.TradingStatus,
            "NORMAL",
            "NORMAL",
            "NORMAL",
            new DateTimeOffset(2007, 11, 15, 0, 0, 0, TimeSpan.Zero),
            null,
            0,
            quote.UpdatedAt);

        return Task.FromResult<SymbolDetailDto?>(detail);
    }

    public Task<SymbolMetadataDto?> GetSymbolMetadataAsync(
        string symbol,
        string boardId,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var normalizedBoardId = NormalizeBoardId(boardId);
        var quote = Quotes.FirstOrDefault(item =>
            item.Symbol.Equals(symbol, StringComparison.OrdinalIgnoreCase) &&
            item.BoardId.Equals(normalizedBoardId, StringComparison.OrdinalIgnoreCase));
        if (quote is null)
        {
            return Task.FromResult<SymbolMetadataDto?>(null);
        }

        var metadata = new SymbolMetadataDto(
            quote.Symbol,
            quote.BoardId,
            quote.MarketId,
            quote.DisplayName,
            quote.DisplayName,
            "Stock",
            "VN000000" + quote.Symbol,
            "STOCK",
            "ST",
            quote.TradingStatus,
            "NORMAL",
            "NORMAL",
            "NORMAL",
            new DateTimeOffset(2007, 11, 15, 0, 0, 0, TimeSpan.Zero),
            null,
            0,
            quote.UpdatedAt);

        return Task.FromResult<SymbolMetadataDto?>(metadata);
    }

    public Task<IReadOnlyList<MarketIndexDto>> GetMarketIndicesAsync(
        IReadOnlyCollection<string> indexNames,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var normalizedIndexNames = NormalizeSymbols(indexNames);
        var indices = Indices
            .Where(index => normalizedIndexNames.Count == 0 || normalizedIndexNames.Contains(index.IndexName))
            .OrderBy(index => index.IndexName, StringComparer.Ordinal)
            .ToArray();

        return Task.FromResult<IReadOnlyList<MarketIndexDto>>(indices);
    }

    public Task<IReadOnlyList<OhlcBarDto>> GetOhlcAsync(
        string symbol,
        string resolution,
        DateTimeOffset? from,
        DateTimeOffset? to,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var normalizedSymbol = symbol.Trim().ToUpperInvariant();
        if (!Quotes.Any(item => item.Symbol == normalizedSymbol))
        {
            return Task.FromResult<IReadOnlyList<OhlcBarDto>>([]);
        }

        OhlcBarDto[] bars =
        [
            new(normalizedSymbol, resolution, SnapshotTime.AddMinutes(-2), 28600m, 28800m, 28550m, 28750m, 420000),
            new(normalizedSymbol, resolution, SnapshotTime.AddMinutes(-1), 28750m, 29100m, 28700m, 29050m, 530000),
            new(normalizedSymbol, resolution, SnapshotTime, 29050m, 29200m, 29000m, 29150m, 610000)
        ];

        return Task.FromResult<IReadOnlyList<OhlcBarDto>>(bars
            .Where(bar => from is null || bar.Time >= from)
            .Where(bar => to is null || bar.Time <= to)
            .ToArray());
    }

    public Task<IReadOnlyList<OhlcBarDto>> GetIndexOhlcAsync(
        string indexName,
        string resolution,
        DateTimeOffset? from,
        DateTimeOffset? to,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var normalizedIndexName = NormalizeToken(indexName);
        var index = Indices.FirstOrDefault(item => item.IndexName == normalizedIndexName);
        if (index is null || index.ReferenceValue is null || index.Value is null)
        {
            return Task.FromResult<IReadOnlyList<OhlcBarDto>>([]);
        }

        OhlcBarDto[] bars =
        [
            new(normalizedIndexName, resolution, SnapshotTime.AddMinutes(-4), index.ReferenceValue.Value, index.ReferenceValue.Value + 2m, index.ReferenceValue.Value - 3m, index.ReferenceValue.Value - 1m, (index.TotalVolume ?? 0) / 5),
            new(normalizedIndexName, resolution, SnapshotTime.AddMinutes(-3), index.ReferenceValue.Value - 1m, index.ReferenceValue.Value + 1m, index.ReferenceValue.Value - 8m, index.ReferenceValue.Value - 5m, (index.TotalVolume ?? 0) / 5),
            new(normalizedIndexName, resolution, SnapshotTime.AddMinutes(-2), index.ReferenceValue.Value - 5m, index.ReferenceValue.Value - 2m, index.ReferenceValue.Value - 10m, index.ReferenceValue.Value - 7m, (index.TotalVolume ?? 0) / 5),
            new(normalizedIndexName, resolution, SnapshotTime.AddMinutes(-1), index.ReferenceValue.Value - 7m, index.ReferenceValue.Value, index.ReferenceValue.Value - 9m, index.Value.Value - 1m, (index.TotalVolume ?? 0) / 5),
            new(normalizedIndexName, resolution, SnapshotTime, index.Value.Value - 1m, index.HighValue ?? index.Value.Value, index.LowValue ?? index.Value.Value, index.Value.Value, (index.TotalVolume ?? 0) / 5)
        ];

        return Task.FromResult<IReadOnlyList<OhlcBarDto>>(bars
            .Where(bar => from is null || bar.Time >= from)
            .Where(bar => to is null || bar.Time <= to)
            .ToArray());
    }

    public Task<IReadOnlyList<MarketTradeDto>> GetLatestTradesAsync(
        string symbol,
        string boardId,
        int limit,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var normalizedSymbol = symbol.Trim().ToUpperInvariant();
        var normalizedBoardId = NormalizeBoardId(boardId);
        if (!Quotes.Any(item => item.Symbol == normalizedSymbol && item.BoardId == normalizedBoardId))
        {
            return Task.FromResult<IReadOnlyList<MarketTradeDto>>([]);
        }

        IReadOnlyList<MarketTradeDto> trades =
        [
            new(normalizedSymbol, normalizedBoardId, SnapshotTime, 29150m, 550m, 1.92m, 2500, 12450000, 362917500000m, "B"),
            new(normalizedSymbol, normalizedBoardId, SnapshotTime.AddSeconds(-20), 29100m, 500m, 1.75m, 1800, 12447500, 362844625000m, "S"),
            new(normalizedSymbol, normalizedBoardId, SnapshotTime.AddSeconds(-45), 29050m, 450m, 1.57m, 3200, 12445700, 362792245000m, string.Empty)
        ];

        return Task.FromResult<IReadOnlyList<MarketTradeDto>>(trades
            .Take(Math.Clamp(limit, 1, 200))
            .ToArray());
    }

    private static string NormalizeBoardId(string boardId)
    {
        return string.IsNullOrWhiteSpace(boardId)
            ? DefaultBoardId
            : boardId.Trim().ToUpperInvariant();
    }

    private static HashSet<string> NormalizeSymbols(IReadOnlyCollection<string> symbols)
    {
        return symbols
            .SelectMany(symbol => symbol.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            .Select(symbol => symbol.Trim().ToUpperInvariant())
            .ToHashSet(StringComparer.Ordinal);
    }

    private static string NormalizeExchange(string? marketId)
    {
        return NormalizeToken(marketId) switch
        {
            "STO" => "HOSE",
            "STX" => "HNX",
            "UPX" => "UPCOM",
            var normalizedMarketId => normalizedMarketId
        };
    }

    private static string NormalizeToken(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : value.Trim().ToUpperInvariant();
    }

    private static bool IsMockIndexMember(string indexName, string symbol)
    {
        if (indexName is "VNINDEX" or "VN30" or "VN100" or "VNXALLSHARE")
        {
            return symbol is "HPG" or "SSI" or "VCB";
        }

        return false;
    }
}
