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
        IReadOnlyCollection<string> symbols,
        string boardId,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var normalizedBoardId = NormalizeBoardId(boardId);
        var symbolFilter = NormalizeSymbols(symbols);

        var quotes = Quotes
            .Where(quote => quote.BoardId.Equals(normalizedBoardId, StringComparison.OrdinalIgnoreCase))
            .Where(quote => symbolFilter.Count == 0 || symbolFilter.Contains(quote.Symbol))
            .OrderBy(quote => quote.Symbol, StringComparer.Ordinal)
            .ToArray();

        return Task.FromResult<IReadOnlyList<MarketQuoteDto>>(quotes);
    }

    public Task<SymbolDetailDto?> GetSymbolDetailAsync(string symbol, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var quote = Quotes.FirstOrDefault(item => item.Symbol.Equals(symbol, StringComparison.OrdinalIgnoreCase));
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
            quote.ReferencePrice,
            quote.CeilingPrice,
            quote.FloorPrice,
            quote.TradingStatus,
            quote.UpdatedAt);

        return Task.FromResult<SymbolDetailDto?>(detail);
    }

    public Task<IReadOnlyList<OhlcBarDto>> GetOhlcAsync(
        string symbol,
        string resolution,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var normalizedSymbol = symbol.Trim().ToUpperInvariant();
        if (!Quotes.Any(item => item.Symbol == normalizedSymbol))
        {
            return Task.FromResult<IReadOnlyList<OhlcBarDto>>([]);
        }

        IReadOnlyList<OhlcBarDto> bars =
        [
            new(normalizedSymbol, resolution, SnapshotTime.AddMinutes(-2), 28600m, 28800m, 28550m, 28750m, 420000),
            new(normalizedSymbol, resolution, SnapshotTime.AddMinutes(-1), 28750m, 29100m, 28700m, 29050m, 530000),
            new(normalizedSymbol, resolution, SnapshotTime, 29050m, 29200m, 29000m, 29150m, 610000)
        ];

        return Task.FromResult(bars);
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
            .Where(symbol => !string.IsNullOrWhiteSpace(symbol))
            .Select(symbol => symbol.Trim().ToUpperInvariant())
            .ToHashSet(StringComparer.Ordinal);
    }
}
