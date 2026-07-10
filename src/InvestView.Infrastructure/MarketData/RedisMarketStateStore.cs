using InvestView.Application.Abstractions.MarketData;
using InvestView.Application.Dtos.MarketData;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace InvestView.Infrastructure.MarketData;

public sealed class RedisMarketStateStore : IMarketStateStore
{
    private readonly IDatabase _database;
    private readonly MarketStateRedisSchema _schema;

    public RedisMarketStateStore(
        IConnectionMultiplexer connectionMultiplexer,
        IOptions<MarketStateOptions> options)
    {
        _database = connectionMultiplexer.GetDatabase();
        _schema = new MarketStateRedisSchema(options.Value);
    }

    public async Task UpsertQuotesAsync(IReadOnlyCollection<MarketQuoteDto> quotes, CancellationToken cancellationToken)
    {
        foreach (var quote in quotes)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var normalizedQuote = MarketStateMapper.NormalizeQuote(quote);
            await StoreQuoteAsync(normalizedQuote, includeGroupTimestamps: true);
        }
    }

    public async Task<MarketQuoteUpdateDto> ApplyQuoteUpdateAsync(MarketQuoteUpdateDto update, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var normalizedUpdate = MarketStateMapper.NormalizeQuoteUpdate(update);
        var key = _schema.QuoteStateKey(normalizedUpdate.BoardId, normalizedUpdate.Symbol);
        var current = await GetQuoteAsync(normalizedUpdate.BoardId, normalizedUpdate.Symbol);
        if (current is not null && normalizedUpdate.UpdatedAt < current.UpdatedAt)
        {
            return MarketStateMapper.CreateQuoteUpdateFromQuote(current);
        }

        var next = current is null
            ? MarketStateMapper.CreateQuoteFromUpdate(normalizedUpdate)
            : MarketStateMapper.MergeQuote(current, normalizedUpdate);

        await _database.HashSetAsync(key, _schema.ToQuoteHash(next, includeGroupTimestamps: false));
        await _database.HashSetAsync(key, _schema.ToQuoteGroupTimestampHash(normalizedUpdate));
        await _database.KeyExpireAsync(key, _schema.QuoteStateTtl);
        await AddQuoteMembershipsAsync(next);

        return normalizedUpdate;
    }

    public async Task<MarketTradeUpdateDto> ApplyTradeUpdateAsync(MarketTradeUpdateDto update, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var normalizedUpdate = MarketStateMapper.NormalizeTradeUpdate(update);
        var tradeKey = _schema.QuoteTradesKey(normalizedUpdate.BoardId, normalizedUpdate.Symbol);
        var trade = MarketStateMapper.CreateTradeFromUpdate(normalizedUpdate);

        await _database.ListLeftPushAsync(tradeKey, _schema.ToTradeMember(trade));
        await _database.ListTrimAsync(tradeKey, 0, 199);
        await _database.KeyExpireAsync(tradeKey, _schema.LatestTradesTtl);
        await ApplyQuoteUpdateAsync(MarketStateMapper.CreateQuoteUpdateFromTrade(normalizedUpdate), cancellationToken);

        return normalizedUpdate;
    }

    public async Task<MarketIndexUpdateDto> ApplyMarketIndexUpdateAsync(MarketIndexUpdateDto update, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var normalizedUpdate = MarketStateMapper.NormalizeIndexUpdate(update);
        var key = _schema.IndexStateKey(normalizedUpdate.IndexName);
        var current = await GetIndexAsync(normalizedUpdate.IndexName);
        if (current is not null && normalizedUpdate.UpdatedAt < current.UpdatedAt)
        {
            return MarketStateMapper.CreateIndexUpdateFromIndex(current);
        }

        var next = current is null
            ? MarketStateMapper.CreateIndexFromUpdate(normalizedUpdate)
            : MarketStateMapper.MergeIndex(current, normalizedUpdate);

        await StoreIndexAsync(next);
        await _database.KeyExpireAsync(key, _schema.QuoteStateTtl);
        return normalizedUpdate;
    }

    public async Task<IReadOnlyList<MarketQuoteDto>> GetQuotesAsync(
        string boardId,
        IReadOnlyCollection<string> symbols,
        CancellationToken cancellationToken)
    {
        var normalizedBoardId = MarketStateMapper.NormalizeBoardId(boardId);
        var normalizedSymbols = MarketStateMapper.NormalizeSymbols(symbols);
        var quoteTasks = normalizedSymbols
            .Select(symbol => _database.HashGetAllAsync(_schema.QuoteStateKey(normalizedBoardId, symbol)))
            .ToArray();

        await Task.WhenAll(quoteTasks);

        return quoteTasks
            .Select(task => _schema.QuoteFromHash(task.Result))
            .Where(quote => quote is not null)
            .Select(quote => quote!)
            .OrderBy(quote => quote.Symbol, StringComparer.Ordinal)
            .ToArray();
    }

    public async Task UpsertSymbolMembershipsAsync(
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
            await AddMembershipAsync(_schema.BoardSymbolsKey(boardId), symbol);
            if (!string.IsNullOrWhiteSpace(marketId))
            {
                await AddMembershipAsync(_schema.MarketSymbolsKey(marketId), symbol);
            }

            if (!string.IsNullOrWhiteSpace(indexName))
            {
                await AddMembershipAsync(_schema.CategorySymbolsKey(indexName), symbol);
            }
        }
    }

    public async Task<IReadOnlyCollection<string>> GetSymbolMembershipsAsync(
        MarketBoardQuery query,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var explicitSymbols = MarketStateMapper.NormalizeSymbols(query.Symbols);
        if (explicitSymbols.Count > 0)
        {
            return explicitSymbols;
        }

        var indexName = MarketStateMapper.Normalize(query.IndexName ?? string.Empty);
        if (!string.IsNullOrWhiteSpace(indexName))
        {
            return await GetMembershipAsync(_schema.CategorySymbolsKey(indexName));
        }

        var marketId = MarketStateMapper.Normalize(query.MarketId ?? string.Empty);
        if (!string.IsNullOrWhiteSpace(marketId))
        {
            return await GetMembershipAsync(_schema.MarketSymbolsKey(marketId));
        }

        return await GetMembershipAsync(_schema.BoardSymbolsKey(query.BoardId));
    }

    public async Task UpsertMarketIndicesAsync(IReadOnlyCollection<MarketIndexDto> indices, CancellationToken cancellationToken)
    {
        foreach (var index in indices)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await StoreIndexAsync(MarketStateMapper.NormalizeIndex(index));
        }
    }

    public async Task<IReadOnlyList<MarketIndexDto>> GetMarketIndicesAsync(
        IReadOnlyCollection<string> indexNames,
        CancellationToken cancellationToken)
    {
        var normalizedNames = MarketStateMapper.NormalizeSymbols(indexNames);
        if (normalizedNames.Count == 0)
        {
            var members = await _database.SetMembersAsync(_schema.IndexNamesKey());
            normalizedNames = members
                .Select(member => MarketStateMapper.Normalize(member!))
                .Where(member => !string.IsNullOrWhiteSpace(member))
                .Distinct(StringComparer.Ordinal)
                .Order(StringComparer.Ordinal)
                .ToArray();
        }

        var indexTasks = normalizedNames
            .Select(indexName => _database.HashGetAllAsync(_schema.IndexStateKey(indexName)))
            .ToArray();

        await Task.WhenAll(indexTasks);

        return indexTasks
            .Select(task => _schema.IndexFromHash(task.Result))
            .Where(index => index is not null)
            .Select(index => index!)
            .OrderBy(index => index.IndexName, StringComparer.Ordinal)
            .ToArray();
    }

    public async Task UpsertSymbolDetailAsync(SymbolDetailDto detail, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var normalizedDetail = NormalizeDetail(detail);
        var key = _schema.SymbolDetailKey(normalizedDetail.BoardId, normalizedDetail.Symbol);

        await _database.HashSetAsync(key, _schema.ToSymbolDetailHash(normalizedDetail));
        await _database.KeyExpireAsync(key, _schema.SymbolDetailTtl);
    }

    public async Task<SymbolDetailDto?> GetSymbolDetailAsync(
        string symbol,
        string boardId,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var values = await _database.HashGetAllAsync(_schema.SymbolDetailKey(boardId, symbol));
        return _schema.SymbolDetailFromHash(values);
    }

    public async Task UpsertOhlcBarsAsync(
        string symbol,
        string resolution,
        DateTimeOffset? from,
        DateTimeOffset? to,
        IReadOnlyCollection<OhlcBarDto> bars,
        CancellationToken cancellationToken)
    {
        await StoreOhlcBarsAsync(
            _schema.OhlcKey(symbol, resolution),
            _schema.OhlcCoverageKey(symbol, resolution),
            from,
            to,
            bars,
            cancellationToken);
    }

    public async Task<IReadOnlyList<OhlcBarDto>> GetOhlcBarsAsync(
        string symbol,
        string resolution,
        DateTimeOffset? from,
        DateTimeOffset? to,
        CancellationToken cancellationToken)
    {
        return await GetOhlcBarsAsync(
            _schema.OhlcKey(symbol, resolution),
            _schema.OhlcCoverageKey(symbol, resolution),
            from,
            to,
            cancellationToken);
    }

    public async Task UpsertIndexOhlcBarsAsync(
        string indexName,
        string resolution,
        DateTimeOffset? from,
        DateTimeOffset? to,
        IReadOnlyCollection<OhlcBarDto> bars,
        CancellationToken cancellationToken)
    {
        await StoreOhlcBarsAsync(
            _schema.IndexOhlcKey(indexName, resolution),
            _schema.IndexOhlcCoverageKey(indexName, resolution),
            from,
            to,
            bars,
            cancellationToken);
    }

    public async Task<IReadOnlyList<OhlcBarDto>> GetIndexOhlcBarsAsync(
        string indexName,
        string resolution,
        DateTimeOffset? from,
        DateTimeOffset? to,
        CancellationToken cancellationToken)
    {
        return await GetOhlcBarsAsync(
            _schema.IndexOhlcKey(indexName, resolution),
            _schema.IndexOhlcCoverageKey(indexName, resolution),
            from,
            to,
            cancellationToken);
    }

    public async Task<IReadOnlyList<MarketTradeDto>> GetLatestTradesAsync(
        string boardId,
        string symbol,
        int limit,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var normalizedLimit = Math.Clamp(limit, 1, 200);
        var values = await _database.ListRangeAsync(
            _schema.QuoteTradesKey(boardId, symbol),
            0,
            normalizedLimit - 1);

        return values
            .Select(_schema.TradeFromMember)
            .Where(trade => trade is not null)
            .Select(trade => trade!)
            .OrderByDescending(trade => trade.Time)
            .ToArray();
    }

    private async Task StoreQuoteAsync(MarketQuoteDto quote, bool includeGroupTimestamps)
    {
        var key = _schema.QuoteStateKey(quote.BoardId, quote.Symbol);
        await _database.HashSetAsync(key, _schema.ToQuoteHash(quote, includeGroupTimestamps));
        await _database.KeyExpireAsync(key, _schema.QuoteStateTtl);
        await AddQuoteMembershipsAsync(quote);
    }

    private async Task AddQuoteMembershipsAsync(MarketQuoteDto quote)
    {
        await AddMembershipAsync(_schema.BoardSymbolsKey(quote.BoardId), quote.Symbol);

        if (!string.IsNullOrWhiteSpace(quote.MarketId))
        {
            await AddMembershipAsync(_schema.MarketSymbolsKey(quote.MarketId), quote.Symbol);
        }
    }

    private async Task AddMembershipAsync(RedisKey key, string symbol)
    {
        var normalizedSymbol = MarketStateMapper.Normalize(symbol);
        if (string.IsNullOrWhiteSpace(normalizedSymbol))
        {
            return;
        }

        await _database.SetAddAsync(key, normalizedSymbol);
        await _database.KeyExpireAsync(key, _schema.MembershipTtl);
    }

    private async Task<IReadOnlyCollection<string>> GetMembershipAsync(RedisKey key)
    {
        var members = await _database.SetMembersAsync(key);
        return members
            .Select(member => MarketStateMapper.Normalize(member!))
            .Where(member => !string.IsNullOrWhiteSpace(member))
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();
    }

    private async Task<MarketQuoteDto?> GetQuoteAsync(string boardId, string symbol)
    {
        var values = await _database.HashGetAllAsync(_schema.QuoteStateKey(boardId, symbol));
        return _schema.QuoteFromHash(values);
    }

    private async Task StoreIndexAsync(MarketIndexDto index)
    {
        var key = _schema.IndexStateKey(index.IndexName);
        await _database.HashSetAsync(key, _schema.ToIndexHash(index));
        await _database.KeyExpireAsync(key, _schema.QuoteStateTtl);

        var indexNamesKey = _schema.IndexNamesKey();
        await _database.SetAddAsync(indexNamesKey, index.IndexName);
        await _database.KeyExpireAsync(indexNamesKey, _schema.MembershipTtl);
    }

    private async Task<MarketIndexDto?> GetIndexAsync(string indexName)
    {
        var values = await _database.HashGetAllAsync(_schema.IndexStateKey(indexName));
        return _schema.IndexFromHash(values);
    }

    private async Task StoreOhlcBarsAsync(
        RedisKey barsKey,
        RedisKey coverageKey,
        DateTimeOffset? from,
        DateTimeOffset? to,
        IReadOnlyCollection<OhlcBarDto> bars,
        CancellationToken cancellationToken)
    {
        foreach (var bar in bars.OrderBy(bar => bar.Time))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var score = _schema.Score(bar.Time);
            await _database.SortedSetRemoveRangeByScoreAsync(barsKey, score, score);
            await _database.SortedSetAddAsync(barsKey, _schema.ToOhlcMember(bar), score);
        }

        await _database.SetAddAsync(coverageKey, _schema.CoverageToken(from, to));
        await _database.KeyExpireAsync(barsKey, _schema.OhlcTtl);
        await _database.KeyExpireAsync(coverageKey, _schema.OhlcTtl);
    }

    private async Task<IReadOnlyList<OhlcBarDto>> GetOhlcBarsAsync(
        RedisKey barsKey,
        RedisKey coverageKey,
        DateTimeOffset? from,
        DateTimeOffset? to,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!await _database.SetContainsAsync(coverageKey, _schema.CoverageToken(from, to)))
        {
            return [];
        }

        var values = await _database.SortedSetRangeByScoreAsync(
            barsKey,
            _schema.MinScore(from),
            _schema.MaxScore(to),
            Exclude.None,
            Order.Ascending);

        return values
            .Select(_schema.OhlcFromMember)
            .Where(bar => bar is not null)
            .Select(bar => bar!)
            .OrderBy(bar => bar.Time)
            .ToArray();
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
}
