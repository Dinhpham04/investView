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
    private readonly ConcurrentDictionary<string, ConcurrentDictionary<long, OhlcBarDto>> _ohlcTimelineBars = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, ConcurrentDictionary<long, OhlcBarDto>> _indexOhlcTimelineBars = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, MarketSessionUpdateDto> _sessions = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, ConcurrentDictionary<string, byte>> _boardSymbols = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, ConcurrentDictionary<string, byte>> _marketSymbols = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, ConcurrentDictionary<string, byte>> _categorySymbols = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, byte> _completeBoardSymbolMemberships = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, byte> _completeMarketSymbolMemberships = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, byte> _completeCategorySymbolMemberships = new(StringComparer.Ordinal);

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
                if (normalizedUpdate.UpdatedAt < current.UpdatedAt &&
                    !MarketStateMapper.IsExpectedOnlyQuoteUpdate(normalizedUpdate))
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

    public Task<MarketOhlcUpdateDto> ApplyOhlcUpdateAsync(MarketOhlcUpdateDto update, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var normalizedUpdate = MarketStateMapper.NormalizeOhlcUpdate(update);
        var bar = MarketStateMapper.CreateOhlcBarFromUpdate(normalizedUpdate);
        var timelines = normalizedUpdate.Type.Equals("INDEX", StringComparison.Ordinal)
            ? _indexOhlcTimelineBars
            : _ohlcTimelineBars;

        UpsertTimelineBars(timelines, normalizedUpdate.Symbol, normalizedUpdate.Resolution, [bar]);
        return Task.FromResult(normalizedUpdate);
    }

    public Task<MarketSessionUpdateDto> ApplyMarketSessionUpdateAsync(
        MarketSessionUpdateDto update,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var normalizedUpdate = MarketStateMapper.NormalizeSessionUpdate(update);
        _sessions[SessionKey(normalizedUpdate.ProductGroupId, normalizedUpdate.BoardId)] = normalizedUpdate;
        return Task.FromResult(normalizedUpdate);
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
        var explicitSymbols = MarketStateMapper.NormalizeSymbols(query.Symbols);
        if (explicitSymbols.Count > 0)
        {
            return Task.CompletedTask;
        }

        var boardId = MarketStateMapper.NormalizeBoardId(query.BoardId);
        var marketId = MarketStateMapper.Normalize(query.MarketId ?? string.Empty);
        var indexName = MarketStateMapper.Normalize(query.IndexName ?? string.Empty);

        if (!string.IsNullOrWhiteSpace(indexName))
        {
            ReplaceMembership(_categorySymbols, indexName, normalizedSymbols);
            MarkMembershipComplete(_completeCategorySymbolMemberships, indexName);
            return Task.CompletedTask;
        }

        if (!string.IsNullOrWhiteSpace(marketId))
        {
            ReplaceMembership(_marketSymbols, marketId, normalizedSymbols);
            MarkMembershipComplete(_completeMarketSymbolMemberships, marketId);
            return Task.CompletedTask;
        }

        ReplaceMembership(_boardSymbols, boardId, normalizedSymbols);
        MarkMembershipComplete(_completeBoardSymbolMemberships, boardId);

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
            return Task.FromResult(GetCompleteMembership(
                _categorySymbols,
                _completeCategorySymbolMemberships,
                indexName));
        }

        var marketId = MarketStateMapper.Normalize(query.MarketId ?? string.Empty);
        if (!string.IsNullOrWhiteSpace(marketId))
        {
            return Task.FromResult(GetCompleteMembership(
                _marketSymbols,
                _completeMarketSymbolMemberships,
                marketId));
        }

        var boardId = MarketStateMapper.NormalizeBoardId(query.BoardId);
        return Task.FromResult(GetCompleteMembership(
            _boardSymbols,
            _completeBoardSymbolMemberships,
            boardId));
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
        var normalizedBars = NormalizeBars(bars);
        _ohlcBars[OhlcKey(symbol, resolution, from, to)] = normalizedBars;
        UpsertTimelineBars(_ohlcTimelineBars, symbol, resolution, normalizedBars);
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
        if (_ohlcBars.TryGetValue(OhlcKey(symbol, resolution, from, to), out var bars))
        {
            return Task.FromResult(bars);
        }

        return Task.FromResult(GetTimelineBars(_ohlcTimelineBars, symbol, resolution, from, to));
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
        var normalizedBars = NormalizeBars(bars);
        _indexOhlcBars[OhlcKey(indexName, resolution, from, to)] = normalizedBars;
        UpsertTimelineBars(_indexOhlcTimelineBars, indexName, resolution, normalizedBars);
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
        if (_indexOhlcBars.TryGetValue(OhlcKey(indexName, resolution, from, to), out var bars))
        {
            return Task.FromResult(bars);
        }

        return Task.FromResult(GetTimelineBars(_indexOhlcTimelineBars, indexName, resolution, from, to));
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

    public Task<MarketSessionUpdateDto?> GetMarketSessionAsync(
        string productGroupId,
        string boardId,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(_sessions.TryGetValue(SessionKey(productGroupId, boardId), out var session) ? session : null);
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

    private static string OhlcTimelineKey(string symbol, string resolution)
    {
        return $"{MarketStateMapper.Normalize(symbol)}:{NormalizeResolution(resolution)}";
    }

    private static void UpsertTimelineBars(
        ConcurrentDictionary<string, ConcurrentDictionary<long, OhlcBarDto>> timelines,
        string symbol,
        string resolution,
        IReadOnlyCollection<OhlcBarDto> bars)
    {
        var timeline = timelines.GetOrAdd(OhlcTimelineKey(symbol, resolution), _ => new ConcurrentDictionary<long, OhlcBarDto>());
        foreach (var bar in NormalizeBars(bars))
        {
            timeline[bar.Time.ToUnixTimeMilliseconds()] = bar;
        }
    }

    private static IReadOnlyList<OhlcBarDto> GetTimelineBars(
        ConcurrentDictionary<string, ConcurrentDictionary<long, OhlcBarDto>> timelines,
        string symbol,
        string resolution,
        DateTimeOffset? from,
        DateTimeOffset? to)
    {
        return timelines.TryGetValue(OhlcTimelineKey(symbol, resolution), out var timeline)
            ? timeline.Values
                .Where(bar => from is null || bar.Time >= from)
                .Where(bar => to is null || bar.Time <= to)
                .OrderBy(bar => bar.Time)
                .ToArray()
            : [];
    }

    private static string SessionKey(string productGroupId, string boardId)
    {
        return $"{MarketStateMapper.Normalize(productGroupId)}:{MarketStateMapper.NormalizeBoardId(boardId)}";
    }

    private static string NormalizeResolution(string resolution)
    {
        return string.IsNullOrWhiteSpace(resolution) ? "1" : resolution.Trim().ToUpperInvariant();
    }

    private static void ReplaceMembership(
        ConcurrentDictionary<string, ConcurrentDictionary<string, byte>> memberships,
        string key,
        IReadOnlyCollection<string> symbols)
    {
        var normalizedKey = MarketStateMapper.Normalize(key);
        if (string.IsNullOrWhiteSpace(normalizedKey))
        {
            return;
        }

        var replacement = new ConcurrentDictionary<string, byte>(StringComparer.Ordinal);
        foreach (var symbol in symbols)
        {
            var normalizedSymbol = MarketStateMapper.Normalize(symbol);
            if (!string.IsNullOrWhiteSpace(normalizedSymbol))
            {
                replacement[normalizedSymbol] = 0;
            }
        }

        memberships[normalizedKey] = replacement;
    }

    private static void MarkMembershipComplete(
        ConcurrentDictionary<string, byte> completeMemberships,
        string key)
    {
        var normalizedKey = MarketStateMapper.Normalize(key);
        if (!string.IsNullOrWhiteSpace(normalizedKey))
        {
            completeMemberships[normalizedKey] = 0;
        }
    }

    private static IReadOnlyCollection<string> GetCompleteMembership(
        ConcurrentDictionary<string, ConcurrentDictionary<string, byte>> memberships,
        ConcurrentDictionary<string, byte> completeMemberships,
        string key)
    {
        var normalizedKey = MarketStateMapper.Normalize(key);
        if (!completeMemberships.ContainsKey(normalizedKey))
        {
            return [];
        }

        return memberships.TryGetValue(normalizedKey, out var symbols)
            ? symbols.Keys.Order(StringComparer.Ordinal).ToArray()
            : [];
    }
}
