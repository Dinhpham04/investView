using System.Collections.Concurrent;
using InvestView.Application.Abstractions.MarketData;
using InvestView.Application.Dtos.MarketData;

namespace InvestView.Infrastructure.MarketData;

public sealed class InMemoryMarketStateStore : IMarketStateStore, IMarketStateMirror
{
    private readonly ConcurrentDictionary<string, MarketQuoteDto> _quotes = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, MarketIndexDto> _indices = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, ConcurrentQueue<MarketTradeDto>> _trades = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, SymbolDetailDto> _symbolDetails = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, IReadOnlyList<OhlcBarDto>> _ohlcBars = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, IReadOnlyList<OhlcBarDto>> _indexOhlcBars = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, ConcurrentDictionary<string, byte>> _boardSymbols = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, ConcurrentDictionary<string, byte>> _marketSymbols = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, ConcurrentDictionary<string, byte>> _categorySymbols = new(StringComparer.Ordinal);

    public Task UpsertQuotesAsync(IReadOnlyCollection<MarketQuoteDto> quotes, CancellationToken cancellationToken)
    {
        foreach (var quote in quotes)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var normalizedQuote = MarketStateMapper.NormalizeQuote(quote);
            _quotes[MarketStateMapper.QuoteKey(normalizedQuote.BoardId, normalizedQuote.Symbol)] = normalizedQuote;
            AddMembership(_boardSymbols, normalizedQuote.BoardId, normalizedQuote.Symbol);
            AddMembership(_marketSymbols, normalizedQuote.MarketId, normalizedQuote.Symbol);
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

    public Task UpsertSymbolMembershipsAsync(
        MarketBoardQuery query,
        IReadOnlyCollection<string> symbols,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var normalizedSymbols = MarketStateMapper.NormalizeSymbols(symbols);
        var boardId = MarketStateMapper.NormalizeBoardId(query.BoardId);
        var marketId = MarketStateMapper.Normalize(query.MarketId ?? string.Empty);
        var indexName = MarketStateMapper.Normalize(query.IndexName ?? string.Empty);

        foreach (var symbol in normalizedSymbols)
        {
            AddMembership(_boardSymbols, boardId, symbol);
            AddMembership(_marketSymbols, marketId, symbol);
            AddMembership(_categorySymbols, indexName, symbol);
        }

        return Task.CompletedTask;
    }

    public Task<IReadOnlyCollection<string>> GetSymbolMembershipsAsync(
        MarketBoardQuery query,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var explicitSymbols = MarketStateMapper.NormalizeSymbols(query.Symbols);
        if (explicitSymbols.Count > 0)
        {
            return Task.FromResult(explicitSymbols);
        }

        var indexName = MarketStateMapper.Normalize(query.IndexName ?? string.Empty);
        if (!string.IsNullOrWhiteSpace(indexName))
        {
            return Task.FromResult(GetMembership(_categorySymbols, indexName));
        }

        var marketId = MarketStateMapper.Normalize(query.MarketId ?? string.Empty);
        if (!string.IsNullOrWhiteSpace(marketId))
        {
            return Task.FromResult(GetMembership(_marketSymbols, marketId));
        }

        return Task.FromResult(GetMembership(_boardSymbols, MarketStateMapper.NormalizeBoardId(query.BoardId)));
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

    public Task UpsertSymbolDetailAsync(SymbolDetailDto detail, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var normalizedDetail = NormalizeDetail(detail);
        _symbolDetails[MarketStateMapper.QuoteKey(normalizedDetail.BoardId, normalizedDetail.Symbol)] = normalizedDetail;
        return Task.CompletedTask;
    }

    public Task<SymbolDetailDto?> GetSymbolDetailAsync(
        string symbol,
        string boardId,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var key = MarketStateMapper.QuoteKey(boardId, symbol);
        return Task.FromResult(_symbolDetails.TryGetValue(key, out var detail) ? detail : null);
    }

    public Task UpsertOhlcBarsAsync(
        string symbol,
        string resolution,
        DateTimeOffset? from,
        DateTimeOffset? to,
        IReadOnlyCollection<OhlcBarDto> bars,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _ohlcBars[OhlcKey(symbol, resolution, from, to)] = NormalizeBars(bars);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<OhlcBarDto>> GetOhlcBarsAsync(
        string symbol,
        string resolution,
        DateTimeOffset? from,
        DateTimeOffset? to,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(_ohlcBars.TryGetValue(OhlcKey(symbol, resolution, from, to), out var bars)
            ? bars
            : []);
    }

    public Task UpsertIndexOhlcBarsAsync(
        string indexName,
        string resolution,
        DateTimeOffset? from,
        DateTimeOffset? to,
        IReadOnlyCollection<OhlcBarDto> bars,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _indexOhlcBars[OhlcKey(indexName, resolution, from, to)] = NormalizeBars(bars);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<OhlcBarDto>> GetIndexOhlcBarsAsync(
        string indexName,
        string resolution,
        DateTimeOffset? from,
        DateTimeOffset? to,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(_indexOhlcBars.TryGetValue(OhlcKey(indexName, resolution, from, to), out var bars)
            ? bars
            : []);
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

    private static SymbolDetailDto NormalizeDetail(SymbolDetailDto detail)
    {
        return detail with
        {
            Symbol = MarketStateMapper.Normalize(detail.Symbol),
            BoardId = MarketStateMapper.NormalizeBoardId(detail.BoardId),
            MarketId = MarketStateMapper.Normalize(detail.MarketId)
        };
    }

    private static IReadOnlyList<OhlcBarDto> NormalizeBars(IReadOnlyCollection<OhlcBarDto> bars)
    {
        return bars
            .Select(bar => bar with
            {
                Symbol = MarketStateMapper.Normalize(bar.Symbol),
                Resolution = NormalizeResolution(bar.Resolution)
            })
            .OrderBy(bar => bar.Time)
            .ToArray();
    }

    private static string OhlcKey(string symbol, string resolution, DateTimeOffset? from, DateTimeOffset? to)
    {
        return $"{MarketStateMapper.Normalize(symbol)}:{NormalizeResolution(resolution)}:{from?.ToUnixTimeSeconds().ToString() ?? string.Empty}:{to?.ToUnixTimeSeconds().ToString() ?? string.Empty}";
    }

    private static string NormalizeResolution(string resolution)
    {
        return string.IsNullOrWhiteSpace(resolution) ? "1" : resolution.Trim().ToUpperInvariant();
    }

    private static void AddMembership(
        ConcurrentDictionary<string, ConcurrentDictionary<string, byte>> memberships,
        string key,
        string symbol)
    {
        var normalizedKey = MarketStateMapper.Normalize(key);
        var normalizedSymbol = MarketStateMapper.Normalize(symbol);
        if (string.IsNullOrWhiteSpace(normalizedKey) || string.IsNullOrWhiteSpace(normalizedSymbol))
        {
            return;
        }

        memberships.GetOrAdd(normalizedKey, _ => new ConcurrentDictionary<string, byte>(StringComparer.Ordinal))[normalizedSymbol] = 0;
    }

    private static IReadOnlyCollection<string> GetMembership(
        ConcurrentDictionary<string, ConcurrentDictionary<string, byte>> memberships,
        string key)
    {
        var normalizedKey = MarketStateMapper.Normalize(key);
        return memberships.TryGetValue(normalizedKey, out var symbols)
            ? symbols.Keys.Order(StringComparer.Ordinal).ToArray()
            : [];
    }
}
