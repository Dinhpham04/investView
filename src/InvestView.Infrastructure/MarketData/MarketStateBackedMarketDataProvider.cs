using InvestView.Application.Abstractions.MarketData;
using InvestView.Application.Dtos.MarketData;
using Microsoft.Extensions.Logging;

namespace InvestView.Infrastructure.MarketData;

public sealed class MarketStateBackedMarketDataProvider : IMarketDataProvider
{
    private readonly IMarketDataProvider _fallbackProvider;
    private readonly IMarketStateMirror _localMirror;
    private readonly IMarketStateStore _sharedStateStore;
    private readonly ILogger<MarketStateBackedMarketDataProvider> _logger;

    public MarketStateBackedMarketDataProvider(
        IMarketDataProvider fallbackProvider,
        IMarketStateMirror localMirror,
        IMarketStateStore sharedStateStore,
        ILogger<MarketStateBackedMarketDataProvider> logger)
    {
        _fallbackProvider = fallbackProvider;
        _localMirror = localMirror;
        _sharedStateStore = sharedStateStore;
        _logger = logger;
    }

    public async Task<IReadOnlyList<MarketQuoteDto>> GetMarketBoardAsync(
        MarketBoardQuery query,
        CancellationToken cancellationToken)
    {
        var normalizedSymbols = NormalizeSymbols(query.Symbols);
        var normalizedBoardId = NormalizeToken(query.BoardId, MockMarketDataProvider.DefaultBoardId);

        if (normalizedSymbols.Count > 0)
        {
            var localQuotes = await _localMirror.GetQuotesAsync(normalizedBoardId, normalizedSymbols, cancellationToken);
            if (HasAllSymbols(localQuotes, normalizedSymbols) && localQuotes.All(IsUsableQuote))
            {
                _logger.LogDebug("Market board snapshot served from local market-state mirror for {BoardId}.", normalizedBoardId);
                return OrderQuotes(localQuotes);
            }

            var sharedQuotes = await _sharedStateStore.GetQuotesAsync(normalizedBoardId, normalizedSymbols, cancellationToken);
            if (HasAllSymbols(sharedQuotes, normalizedSymbols) && sharedQuotes.All(IsUsableQuote))
            {
                _logger.LogDebug("Market board snapshot served from shared market-state store for {BoardId}.", normalizedBoardId);
                await _localMirror.UpsertQuotesAsync(sharedQuotes, cancellationToken);
                return OrderQuotes(sharedQuotes);
            }
        }

        _logger.LogDebug("Market board snapshot falling back to REST provider for {BoardId}.", normalizedBoardId);
        var fallbackQuotes = await _fallbackProvider.GetMarketBoardAsync(query, cancellationToken);
        await _sharedStateStore.UpsertQuotesAsync(fallbackQuotes, cancellationToken);
        await _localMirror.UpsertQuotesAsync(fallbackQuotes, cancellationToken);
        return OrderQuotes(fallbackQuotes);
    }

    public async Task<SymbolDetailDto?> GetSymbolDetailAsync(
        string symbol,
        string boardId,
        CancellationToken cancellationToken)
    {
        return await _fallbackProvider.GetSymbolDetailAsync(symbol, boardId, cancellationToken);
    }

    public async Task<IReadOnlyList<OhlcBarDto>> GetOhlcAsync(
        string symbol,
        string resolution,
        DateTimeOffset? from,
        DateTimeOffset? to,
        CancellationToken cancellationToken)
    {
        return await _fallbackProvider.GetOhlcAsync(symbol, resolution, from, to, cancellationToken);
    }

    public async Task<IReadOnlyList<MarketIndexDto>> GetMarketIndicesAsync(
        IReadOnlyCollection<string> indexNames,
        CancellationToken cancellationToken)
    {
        var normalizedNames = NormalizeSymbols(indexNames);
        var localIndices = await _localMirror.GetMarketIndicesAsync(normalizedNames, cancellationToken);
        if (normalizedNames.Count == 0 ? localIndices.Count > 0 : HasAllIndices(localIndices, normalizedNames))
        {
            return OrderIndices(localIndices);
        }

        var sharedIndices = await _sharedStateStore.GetMarketIndicesAsync(normalizedNames, cancellationToken);
        if (normalizedNames.Count == 0 ? sharedIndices.Count > 0 : HasAllIndices(sharedIndices, normalizedNames))
        {
            await _localMirror.UpsertMarketIndicesAsync(sharedIndices, cancellationToken);
            return OrderIndices(sharedIndices);
        }

        var fallbackIndices = await _fallbackProvider.GetMarketIndicesAsync(normalizedNames, cancellationToken);
        await _sharedStateStore.UpsertMarketIndicesAsync(fallbackIndices, cancellationToken);
        await _localMirror.UpsertMarketIndicesAsync(fallbackIndices, cancellationToken);
        return OrderIndices(fallbackIndices);
    }

    public async Task<IReadOnlyList<OhlcBarDto>> GetIndexOhlcAsync(
        string indexName,
        string resolution,
        DateTimeOffset? from,
        DateTimeOffset? to,
        CancellationToken cancellationToken)
    {
        return await _fallbackProvider.GetIndexOhlcAsync(indexName, resolution, from, to, cancellationToken);
    }

    public async Task<IReadOnlyList<MarketTradeDto>> GetLatestTradesAsync(
        string symbol,
        string boardId,
        int limit,
        CancellationToken cancellationToken)
    {
        var normalizedSymbol = NormalizeToken(symbol, string.Empty);
        var normalizedBoardId = NormalizeToken(boardId, MockMarketDataProvider.DefaultBoardId);
        var normalizedLimit = Math.Clamp(limit, 1, 200);

        var localTrades = await _localMirror.GetLatestTradesAsync(normalizedBoardId, normalizedSymbol, normalizedLimit, cancellationToken);
        if (localTrades.Count > 0)
        {
            return localTrades;
        }

        var sharedTrades = await _sharedStateStore.GetLatestTradesAsync(normalizedBoardId, normalizedSymbol, normalizedLimit, cancellationToken);
        if (sharedTrades.Count > 0)
        {
            foreach (var trade in sharedTrades)
            {
                await _localMirror.ApplyTradeUpdateAsync(ToUpdate(trade), cancellationToken);
            }

            return sharedTrades;
        }

        var fallbackTrades = await _fallbackProvider.GetLatestTradesAsync(normalizedSymbol, normalizedBoardId, normalizedLimit, cancellationToken);
        foreach (var trade in fallbackTrades)
        {
            var update = ToUpdate(trade);
            await _sharedStateStore.ApplyTradeUpdateAsync(update, cancellationToken);
            await _localMirror.ApplyTradeUpdateAsync(update, cancellationToken);
        }

        return fallbackTrades;
    }

    private static MarketTradeUpdateDto ToUpdate(MarketTradeDto trade)
    {
        return new MarketTradeUpdateDto(
            trade.Symbol,
            trade.BoardId,
            trade.Time,
            trade.Price,
            trade.Change,
            trade.ChangePercent,
            trade.Quantity,
            trade.TotalVolume,
            trade.TotalValue,
            trade.Side);
    }

    private static bool HasAllSymbols(IReadOnlyCollection<MarketQuoteDto> quotes, IReadOnlyCollection<string> symbols)
    {
        var quoteSymbols = quotes.Select(quote => quote.Symbol).ToHashSet(StringComparer.Ordinal);
        return symbols.All(quoteSymbols.Contains);
    }

    private static bool IsUsableQuote(MarketQuoteDto quote)
    {
        return quote.ReferencePrice > 0m && quote.CeilingPrice > 0m && quote.FloorPrice > 0m;
    }

    private static bool HasAllIndices(IReadOnlyCollection<MarketIndexDto> indices, IReadOnlyCollection<string> indexNames)
    {
        var availableNames = indices.Select(index => index.IndexName).ToHashSet(StringComparer.Ordinal);
        return indexNames.All(availableNames.Contains);
    }

    private static IReadOnlyList<MarketQuoteDto> OrderQuotes(IEnumerable<MarketQuoteDto> quotes)
    {
        return quotes.OrderBy(quote => quote.Symbol, StringComparer.Ordinal).ToArray();
    }

    private static IReadOnlyList<MarketIndexDto> OrderIndices(IEnumerable<MarketIndexDto> indices)
    {
        return indices.OrderBy(index => index.IndexName, StringComparer.Ordinal).ToArray();
    }

    private static IReadOnlyCollection<string> NormalizeSymbols(IReadOnlyCollection<string> symbols)
    {
        return symbols
            .SelectMany(symbol => symbol.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            .Select(symbol => NormalizeToken(symbol, string.Empty))
            .Where(symbol => !string.IsNullOrWhiteSpace(symbol))
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();
    }

    private static string NormalizeToken(string value, string fallback)
    {
        return string.IsNullOrWhiteSpace(value) ? fallback : value.Trim().ToUpperInvariant();
    }
}
