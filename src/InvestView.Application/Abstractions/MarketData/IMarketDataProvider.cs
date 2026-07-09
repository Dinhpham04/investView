using InvestView.Application.Dtos.MarketData;

namespace InvestView.Application.Abstractions.MarketData;

public interface IMarketDataProvider
{
    Task<IReadOnlyList<MarketQuoteDto>> GetMarketBoardAsync(
        MarketBoardQuery query,
        CancellationToken cancellationToken);

    Task<SymbolDetailDto?> GetSymbolDetailAsync(
        string symbol,
        string boardId,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<MarketIndexDto>> GetMarketIndicesAsync(
        IReadOnlyCollection<string> indexNames,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<OhlcBarDto>> GetOhlcAsync(
        string symbol,
        string resolution,
        DateTimeOffset? from,
        DateTimeOffset? to,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<OhlcBarDto>> GetIndexOhlcAsync(
        string indexName,
        string resolution,
        DateTimeOffset? from,
        DateTimeOffset? to,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<MarketTradeDto>> GetLatestTradesAsync(
        string symbol,
        string boardId,
        int limit,
        CancellationToken cancellationToken);
}
