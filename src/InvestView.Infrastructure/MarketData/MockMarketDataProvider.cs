using InvestView.Application.Abstractions.MarketData;
using InvestView.Application.Dtos.MarketData;

namespace InvestView.Infrastructure.MarketData;

public sealed class MockMarketDataProvider : IMarketDataProvider
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
