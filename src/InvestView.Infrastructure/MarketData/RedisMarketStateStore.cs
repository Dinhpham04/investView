using InvestView.Application.Abstractions.MarketData;
using InvestView.Application.Dtos.MarketData;
using Microsoft.Extensions.Options;
using StackExchange.Redis;
using System.Globalization;

namespace InvestView.Infrastructure.MarketData;

public sealed class RedisMarketStateStore : IMarketStateStore
{
    private const string CoverageFromField = "from";
    private const string CoverageToField = "to";

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
        if (current is not null &&
            normalizedUpdate.UpdatedAt < current.UpdatedAt &&
            !MarketStateMapper.IsExpectedOnlyQuoteUpdate(normalizedUpdate))
        {
            return MarketStateMapper.CreateQuoteUpdateFromQuote(current);
        }

        var next = current is null
            ? MarketStateMapper.CreateQuoteFromUpdate(normalizedUpdate)
            : MarketStateMapper.MergeQuote(current, normalizedUpdate);

        await _database.HashSetAsync(key, _schema.ToQuoteHash(next, includeGroupTimestamps: false));
        await DeleteNullQuoteHashFieldsAsync(key, next);
        await _database.HashSetAsync(key, _schema.ToQuoteGroupTimestampHash(normalizedUpdate));
        await _database.KeyExpireAsync(key, _schema.QuoteStateTtl);

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

    public async Task<MarketOhlcUpdateDto> ApplyOhlcUpdateAsync(
        MarketOhlcUpdateDto update,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var normalizedUpdate = MarketStateMapper.NormalizeOhlcUpdate(update);
        var bar = MarketStateMapper.CreateOhlcBarFromUpdate(normalizedUpdate);
        var isIndex = normalizedUpdate.Type.Equals("INDEX", StringComparison.Ordinal);
        var barsKey = isIndex
            ? _schema.IndexOhlcKey(normalizedUpdate.Symbol, normalizedUpdate.Resolution)
            : _schema.OhlcKey(normalizedUpdate.Symbol, normalizedUpdate.Resolution);
        var score = _schema.Score(bar.Time);

        await _database.SortedSetRemoveRangeByScoreAsync(barsKey, score, score);
        await _database.SortedSetAddAsync(barsKey, _schema.ToOhlcMember(bar), score);
        await _database.KeyExpireAsync(barsKey, _schema.OhlcTtl);
        if (isIndex)
        {
            await ExtendOhlcCoverageRangeAsync(
                _schema.IndexOhlcCoverageRangeKey(normalizedUpdate.Symbol, normalizedUpdate.Resolution),
                bar.Time,
                bar.Time,
                createWhenMissing: false);
        }

        return normalizedUpdate;
    }

    public async Task<MarketSessionUpdateDto> ApplyMarketSessionUpdateAsync(
        MarketSessionUpdateDto update,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var normalizedUpdate = MarketStateMapper.NormalizeSessionUpdate(update);
        var key = _schema.SessionStateKey(normalizedUpdate.ProductGroupId, normalizedUpdate.BoardId);

        await _database.HashSetAsync(key, _schema.ToSessionHash(normalizedUpdate));
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
        var explicitSymbols = MarketStateMapper.NormalizeSymbols(query.Symbols);
        if (explicitSymbols.Count > 0)
        {
            return;
        }

        var boardId = MarketStateMapper.NormalizeBoardId(query.BoardId);
        var marketId = MarketStateMapper.Normalize(query.MarketId ?? string.Empty);
        var indexName = MarketStateMapper.Normalize(query.IndexName ?? string.Empty);

        if (!string.IsNullOrWhiteSpace(indexName))
        {
            await ReplaceMembershipAsync(_schema.CategorySymbolsKey(indexName), normalizedSymbols);
            return;
        }

        if (!string.IsNullOrWhiteSpace(marketId))
        {
            await ReplaceMembershipAsync(_schema.MarketSymbolsKey(marketId), normalizedSymbols);
            return;
        }

        await ReplaceMembershipAsync(_schema.BoardSymbolsKey(boardId), normalizedSymbols);
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
            return await GetCompleteMembershipAsync(_schema.CategorySymbolsKey(indexName));
        }

        var marketId = MarketStateMapper.Normalize(query.MarketId ?? string.Empty);
        if (!string.IsNullOrWhiteSpace(marketId))
        {
            return await GetCompleteMembershipAsync(_schema.MarketSymbolsKey(marketId));
        }

        return await GetCompleteMembershipAsync(_schema.BoardSymbolsKey(query.BoardId));
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

    public async Task<bool> HasOhlcCoverageAsync(
        string symbol,
        string resolution,
        DateTimeOffset? from,
        DateTimeOffset? to,
        CancellationToken cancellationToken)
    {
        return await HasOhlcCoverageAsync(
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
        await ExtendOhlcCoverageRangeAsync(
            _schema.IndexOhlcCoverageRangeKey(indexName, resolution),
            from ?? bars.OrderBy(bar => bar.Time).FirstOrDefault()?.Time,
            to ?? bars.OrderBy(bar => bar.Time).LastOrDefault()?.Time);
    }

    public async Task<IReadOnlyList<OhlcBarDto>> GetIndexOhlcBarsAsync(
        string indexName,
        string resolution,
        DateTimeOffset? from,
        DateTimeOffset? to,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!await HasIndexOhlcCoverageUntilAsync(indexName, resolution, from, to, cancellationToken))
        {
            return [];
        }

        return await ReadOhlcBarsAsync(_schema.IndexOhlcKey(indexName, resolution), from, to);
    }

    public async Task<bool> HasIndexOhlcCoverageAsync(
        string indexName,
        string resolution,
        DateTimeOffset? from,
        DateTimeOffset? to,
        CancellationToken cancellationToken)
    {
        return await HasOhlcCoverageAsync(
            _schema.IndexOhlcCoverageKey(indexName, resolution),
            from,
            to,
            cancellationToken);
    }

    public async Task<bool> HasIndexOhlcCoverageUntilAsync(
        string indexName,
        string resolution,
        DateTimeOffset? from,
        DateTimeOffset? to,
        CancellationToken cancellationToken)
    {
        if (await HasOhlcCoverageAsync(
                _schema.IndexOhlcCoverageKey(indexName, resolution),
                from,
                to,
                cancellationToken))
        {
            return true;
        }

        return await HasOhlcCoverageRangeAsync(
            _schema.IndexOhlcCoverageRangeKey(indexName, resolution),
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

    public async Task<MarketSessionUpdateDto?> GetMarketSessionAsync(
        string productGroupId,
        string boardId,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var values = await _database.HashGetAllAsync(_schema.SessionStateKey(productGroupId, boardId));
        return _schema.SessionFromHash(values);
    }

    private async Task StoreQuoteAsync(MarketQuoteDto quote, bool includeGroupTimestamps)
    {
        var key = _schema.QuoteStateKey(quote.BoardId, quote.Symbol);
        await _database.HashSetAsync(key, _schema.ToQuoteHash(quote, includeGroupTimestamps));
        await DeleteNullQuoteHashFieldsAsync(key, quote);
        await _database.KeyExpireAsync(key, _schema.QuoteStateTtl);
    }

    private async Task DeleteNullQuoteHashFieldsAsync(RedisKey key, MarketQuoteDto quote)
    {
        var fields = _schema.ToQuoteHashFieldsToDelete(quote);
        if (fields.Length > 0)
        {
            await _database.HashDeleteAsync(key, fields);
        }
    }

    private async Task ReplaceMembershipAsync(RedisKey key, IReadOnlyCollection<string> symbols)
    {
        var normalizedSymbols = MarketStateMapper.NormalizeSymbols(symbols);
        var values = normalizedSymbols
            .Select(symbol => (RedisValue)symbol)
            .ToArray();

        await _database.KeyDeleteAsync(key);
        if (values.Length > 0)
        {
            await _database.SetAddAsync(key, values);
            await _database.KeyExpireAsync(key, _schema.MembershipTtl);
        }

        var coverageKey = _schema.SymbolMembershipCoverageKey(key);
        await _database.StringSetAsync(coverageKey, "1", _schema.MembershipTtl);
    }

    private async Task<IReadOnlyCollection<string>> GetCompleteMembershipAsync(RedisKey key)
    {
        var coverageKey = _schema.SymbolMembershipCoverageKey(key);
        if (!await _database.KeyExistsAsync(coverageKey))
        {
            return [];
        }

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
        if (!await HasOhlcCoverageAsync(coverageKey, from, to, cancellationToken))
        {
            return [];
        }

        return await ReadOhlcBarsAsync(barsKey, from, to);
    }

    private async Task<IReadOnlyList<OhlcBarDto>> ReadOhlcBarsAsync(
        RedisKey barsKey,
        DateTimeOffset? from,
        DateTimeOffset? to)
    {
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

    private async Task<bool> HasOhlcCoverageAsync(
        RedisKey coverageKey,
        DateTimeOffset? from,
        DateTimeOffset? to,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return await _database.SetContainsAsync(coverageKey, _schema.CoverageToken(from, to));
    }

    private async Task ExtendOhlcCoverageRangeAsync(
        RedisKey coverageRangeKey,
        DateTimeOffset? from,
        DateTimeOffset? to,
        bool createWhenMissing = true)
    {
        if (!from.HasValue && !to.HasValue)
        {
            return;
        }

        var entries = await _database.HashGetAllAsync(coverageRangeKey);
        if (!createWhenMissing && entries.Length == 0)
        {
            return;
        }

        var currentFrom = ReadCoverageTimestamp(entries, CoverageFromField);
        var currentTo = ReadCoverageTimestamp(entries, CoverageToField);
        if (!createWhenMissing && (!currentFrom.HasValue || !currentTo.HasValue))
        {
            return;
        }

        var nextFrom = Min(currentFrom, from ?? to);
        var nextTo = Max(currentTo, to ?? from);
        var hashEntries = new List<HashEntry>(2);
        if (nextFrom.HasValue)
        {
            hashEntries.Add(new HashEntry(
                CoverageFromField,
                nextFrom.Value.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture)));
        }

        if (nextTo.HasValue)
        {
            hashEntries.Add(new HashEntry(
                CoverageToField,
                nextTo.Value.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture)));
        }

        if (hashEntries.Count == 0)
        {
            return;
        }

        await _database.HashSetAsync(coverageRangeKey, hashEntries.ToArray());
        await _database.KeyExpireAsync(coverageRangeKey, _schema.OhlcTtl);
    }

    private async Task<bool> HasOhlcCoverageRangeAsync(
        RedisKey coverageRangeKey,
        DateTimeOffset? from,
        DateTimeOffset? to,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var entries = await _database.HashGetAllAsync(coverageRangeKey);
        var coveredFrom = ReadCoverageTimestamp(entries, CoverageFromField);
        var coveredTo = ReadCoverageTimestamp(entries, CoverageToField);
        var coversFrom = from is null || (coveredFrom.HasValue && coveredFrom.Value <= from.Value);
        var coversTo = to is null || (coveredTo.HasValue && coveredTo.Value >= to.Value);
        return coversFrom && coversTo;
    }

    private static DateTimeOffset? ReadCoverageTimestamp(HashEntry[] entries, string field)
    {
        var value = entries.FirstOrDefault(entry => entry.Name == field).Value;
        if (!value.HasValue ||
            !long.TryParse((string)value!, NumberStyles.Integer, CultureInfo.InvariantCulture, out var unixSeconds))
        {
            return null;
        }

        return DateTimeOffset.FromUnixTimeSeconds(unixSeconds);
    }

    private static DateTimeOffset? Min(DateTimeOffset? left, DateTimeOffset? right)
    {
        if (!left.HasValue)
        {
            return right;
        }

        if (!right.HasValue)
        {
            return left;
        }

        return left.Value <= right.Value ? left : right;
    }

    private static DateTimeOffset? Max(DateTimeOffset? left, DateTimeOffset? right)
    {
        if (!left.HasValue)
        {
            return right;
        }

        if (!right.HasValue)
        {
            return left;
        }

        return left.Value >= right.Value ? left : right;
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
