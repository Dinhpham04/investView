using System.Collections.Concurrent;
using InvestView.Application.Abstractions.MarketData;
using InvestView.Application.Dtos.MarketData;

namespace InvestView.Infrastructure.MarketData;

public sealed class InMemoryMarketStateStore : IMarketStateStore, IMarketStateMirror
{
    private readonly ConcurrentDictionary<string, MarketQuoteDto> _quotes = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, MarketIndexDto> _indices = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, ConcurrentQueue<MarketTradeDto>> _trades = new(StringComparer.Ordinal);

    public Task UpsertQuotesAsync(IReadOnlyCollection<MarketQuoteDto> quotes, CancellationToken cancellationToken)
    {
        foreach (var quote in quotes)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var normalizedQuote = MarketStateMapper.NormalizeQuote(quote);
            _quotes[MarketStateMapper.QuoteKey(normalizedQuote.BoardId, normalizedQuote.Symbol)] = normalizedQuote;
        }

        return Task.CompletedTask;
    }

    public Task<MarketQuoteUpdateDto> ApplyQuoteUpdateAsync(MarketQuoteUpdateDto update, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var normalizedUpdate = MarketStateMapper.NormalizeQuoteUpdate(update);
        var key = MarketStateMapper.QuoteKey(normalizedUpdate.BoardId, normalizedUpdate.Symbol);
        var resultUpdate = normalizedUpdate;

        _quotes.AddOrUpdate(
            key,
            _ => MarketStateMapper.CreateQuoteFromUpdate(normalizedUpdate),
            (_, current) =>
            {
                if (normalizedUpdate.UpdatedAt < current.UpdatedAt)
                {
                    resultUpdate = MarketStateMapper.CreateQuoteUpdateFromQuote(current);
                    return current;
                }

                return MarketStateMapper.MergeQuote(current, normalizedUpdate);
            });

        return Task.FromResult(resultUpdate);
    }

    public async Task<MarketTradeUpdateDto> ApplyTradeUpdateAsync(MarketTradeUpdateDto update, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var normalizedUpdate = MarketStateMapper.NormalizeTradeUpdate(update);
        var key = MarketStateMapper.QuoteKey(normalizedUpdate.BoardId, normalizedUpdate.Symbol);
        var queue = _trades.GetOrAdd(key, _ => new ConcurrentQueue<MarketTradeDto>());

        queue.Enqueue(MarketStateMapper.CreateTradeFromUpdate(normalizedUpdate));

        while (queue.Count > 100 && queue.TryDequeue(out _))
        {
        }

        await ApplyQuoteUpdateAsync(MarketStateMapper.CreateQuoteUpdateFromTrade(normalizedUpdate), cancellationToken);
        return normalizedUpdate;
    }

    public Task<MarketIndexUpdateDto> ApplyMarketIndexUpdateAsync(MarketIndexUpdateDto update, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var normalizedUpdate = MarketStateMapper.NormalizeIndexUpdate(update);
        var resultUpdate = normalizedUpdate;

        _indices.AddOrUpdate(
            normalizedUpdate.IndexName,
            _ => MarketStateMapper.CreateIndexFromUpdate(normalizedUpdate),
            (_, current) =>
            {
                if (normalizedUpdate.UpdatedAt < current.UpdatedAt)
                {
                    resultUpdate = MarketStateMapper.CreateIndexUpdateFromIndex(current);
                    return current;
                }

                return MarketStateMapper.MergeIndex(current, normalizedUpdate);
            });

        return Task.FromResult(resultUpdate);
    }

    public Task<IReadOnlyList<MarketQuoteDto>> GetQuotesAsync(
        string boardId,
        IReadOnlyCollection<string> symbols,
        CancellationToken cancellationToken)
    {
        var normalizedBoardId = MarketStateMapper.NormalizeBoardId(boardId);
        var normalizedSymbols = MarketStateMapper.NormalizeSymbols(symbols);
        var quotes = normalizedSymbols
            .Select(symbol => _quotes.TryGetValue(MarketStateMapper.QuoteKey(normalizedBoardId, symbol), out var quote) ? quote : null)
            .Where(quote => quote is not null)
            .Select(quote => quote!)
            .OrderBy(quote => quote.Symbol, StringComparer.Ordinal)
            .ToArray();

        return Task.FromResult<IReadOnlyList<MarketQuoteDto>>(quotes);
    }

    public Task UpsertMarketIndicesAsync(IReadOnlyCollection<MarketIndexDto> indices, CancellationToken cancellationToken)
    {
        foreach (var index in indices)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var normalizedIndex = MarketStateMapper.NormalizeIndex(index);
            _indices[normalizedIndex.IndexName] = normalizedIndex;
        }

        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<MarketIndexDto>> GetMarketIndicesAsync(
        IReadOnlyCollection<string> indexNames,
        CancellationToken cancellationToken)
    {
        var normalizedNames = MarketStateMapper.NormalizeSymbols(indexNames);
        var indices = normalizedNames.Count == 0
            ? _indices.Values.ToArray()
            : normalizedNames
                .Select(indexName => _indices.TryGetValue(indexName, out var index) ? index : null)
                .Where(index => index is not null)
                .Select(index => index!)
                .ToArray();

        return Task.FromResult<IReadOnlyList<MarketIndexDto>>(indices.OrderBy(index => index.IndexName, StringComparer.Ordinal).ToArray());
    }

    public Task<IReadOnlyList<MarketTradeDto>> GetLatestTradesAsync(
        string boardId,
        string symbol,
        int limit,
        CancellationToken cancellationToken)
    {
        var key = MarketStateMapper.QuoteKey(boardId, symbol);
        if (!_trades.TryGetValue(key, out var queue))
        {
            return Task.FromResult<IReadOnlyList<MarketTradeDto>>([]);
        }

        var normalizedLimit = Math.Clamp(limit, 1, 200);
        var trades = queue
            .ToArray()
            .OrderByDescending(trade => trade.Time)
            .Take(normalizedLimit)
            .ToArray();

        return Task.FromResult<IReadOnlyList<MarketTradeDto>>(trades);
    }
}
