using InvestView.Application.Dtos.MarketData;

namespace InvestView.Application.Abstractions.MarketData;

public interface IMarketStateStore
{
    Task UpsertQuotesAsync(IReadOnlyCollection<MarketQuoteDto> quotes, CancellationToken cancellationToken);

    Task<MarketQuoteUpdateDto> ApplyQuoteUpdateAsync(MarketQuoteUpdateDto update, CancellationToken cancellationToken);

    Task<MarketTradeUpdateDto> ApplyTradeUpdateAsync(MarketTradeUpdateDto update, CancellationToken cancellationToken);

    Task<MarketIndexUpdateDto> ApplyMarketIndexUpdateAsync(MarketIndexUpdateDto update, CancellationToken cancellationToken);

    Task<MarketOhlcUpdateDto> ApplyOhlcUpdateAsync(MarketOhlcUpdateDto update, CancellationToken cancellationToken);

    Task<MarketSessionUpdateDto> ApplyMarketSessionUpdateAsync(MarketSessionUpdateDto update, CancellationToken cancellationToken);

    Task<IReadOnlyList<MarketQuoteDto>> GetQuotesAsync(
        string boardId,
        IReadOnlyCollection<string> symbols,
        CancellationToken cancellationToken);

    Task UpsertSymbolMembershipsAsync(
        MarketBoardQuery query,
        IReadOnlyCollection<string> symbols,
        CancellationToken cancellationToken);

    Task<IReadOnlyCollection<string>> GetSymbolMembershipsAsync(
        MarketBoardQuery query,
        CancellationToken cancellationToken);

    Task UpsertMarketIndicesAsync(IReadOnlyCollection<MarketIndexDto> indices, CancellationToken cancellationToken);

    Task<IReadOnlyList<MarketIndexDto>> GetMarketIndicesAsync(
        IReadOnlyCollection<string> indexNames,
        CancellationToken cancellationToken);

    Task UpsertSymbolDetailAsync(SymbolDetailDto detail, CancellationToken cancellationToken);

    Task<SymbolDetailDto?> GetSymbolDetailAsync(
        string symbol,
        string boardId,
        CancellationToken cancellationToken);

    Task UpsertOhlcBarsAsync(
        string symbol,
        string resolution,
        DateTimeOffset? from,
        DateTimeOffset? to,
        IReadOnlyCollection<OhlcBarDto> bars,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<OhlcBarDto>> GetOhlcBarsAsync(
        string symbol,
        string resolution,
        DateTimeOffset? from,
        DateTimeOffset? to,
        CancellationToken cancellationToken);

    Task<bool> HasOhlcCoverageAsync(
        string symbol,
        string resolution,
        DateTimeOffset? from,
        DateTimeOffset? to,
        CancellationToken cancellationToken);

    Task UpsertIndexOhlcBarsAsync(
        string indexName,
        string resolution,
        DateTimeOffset? from,
        DateTimeOffset? to,
        IReadOnlyCollection<OhlcBarDto> bars,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<OhlcBarDto>> GetIndexOhlcBarsAsync(
        string indexName,
        string resolution,
        DateTimeOffset? from,
        DateTimeOffset? to,
        CancellationToken cancellationToken);

    Task<bool> HasIndexOhlcCoverageAsync(
        string indexName,
        string resolution,
        DateTimeOffset? from,
        DateTimeOffset? to,
        CancellationToken cancellationToken);

    Task<bool> HasIndexOhlcCoverageUntilAsync(
        string indexName,
        string resolution,
        DateTimeOffset? from,
        DateTimeOffset? to,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<MarketTradeDto>> GetLatestTradesAsync(
        string boardId,
        string symbol,
        int limit,
        CancellationToken cancellationToken);

    Task<MarketSessionUpdateDto?> GetMarketSessionAsync(
        string productGroupId,
        string boardId,
        CancellationToken cancellationToken);
}

public interface IMarketStateMirror : IMarketStateStore
{
}
