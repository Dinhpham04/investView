using System.Text.Json;
using InvestView.Application.Abstractions.MarketData;
using InvestView.Application.Dtos.MarketData;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace InvestView.Infrastructure.MarketData;

public sealed class RedisMarketStateStore : IMarketStateStore
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    private readonly IDatabase _database;
    private readonly MarketStateOptions _options;

    public RedisMarketStateStore(
        IConnectionMultiplexer connectionMultiplexer,
        IOptions<MarketStateOptions> options)
    {
        _database = connectionMultiplexer.GetDatabase();
        _options = options.Value;
    }

    public async Task UpsertQuotesAsync(IReadOnlyCollection<MarketQuoteDto> quotes, CancellationToken cancellationToken)
    {
        foreach (var quote in quotes)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var normalizedQuote = MarketStateMapper.NormalizeQuote(quote);
            await _database.StringSetAsync(
                QuoteRedisKey(normalizedQuote.BoardId, normalizedQuote.Symbol),
                Serialize(normalizedQuote),
                EffectiveTtl());
            await _database.SetAddAsync(BoardSymbolsRedisKey(normalizedQuote.BoardId), normalizedQuote.Symbol);
            await _database.KeyExpireAsync(BoardSymbolsRedisKey(normalizedQuote.BoardId), EffectiveTtl());
        }
    }

    public async Task<MarketQuoteUpdateDto> ApplyQuoteUpdateAsync(MarketQuoteUpdateDto update, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var normalizedUpdate = MarketStateMapper.NormalizeQuoteUpdate(update);
        var key = QuoteRedisKey(normalizedUpdate.BoardId, normalizedUpdate.Symbol);
        var current = await GetValueAsync<MarketQuoteDto>(key);
        if (current is not null && normalizedUpdate.UpdatedAt < current.UpdatedAt)
        {
            return MarketStateMapper.CreateQuoteUpdateFromQuote(current);
        }

        var next = current is null
            ? MarketStateMapper.CreateQuoteFromUpdate(normalizedUpdate)
            : MarketStateMapper.MergeQuote(current, normalizedUpdate);

        await _database.StringSetAsync(key, Serialize(next), EffectiveTtl());
        await _database.SetAddAsync(BoardSymbolsRedisKey(normalizedUpdate.BoardId), normalizedUpdate.Symbol);
        await _database.KeyExpireAsync(BoardSymbolsRedisKey(normalizedUpdate.BoardId), EffectiveTtl());
        return normalizedUpdate;
    }

    public async Task<MarketTradeUpdateDto> ApplyTradeUpdateAsync(MarketTradeUpdateDto update, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var normalizedUpdate = MarketStateMapper.NormalizeTradeUpdate(update);
        var tradeKey = TradeRedisKey(normalizedUpdate.BoardId, normalizedUpdate.Symbol);
        var trade = MarketStateMapper.CreateTradeFromUpdate(normalizedUpdate);

        await _database.ListLeftPushAsync(tradeKey, Serialize(trade));
        await _database.ListTrimAsync(tradeKey, 0, 99);
        await _database.KeyExpireAsync(tradeKey, EffectiveTtl());
        await ApplyQuoteUpdateAsync(MarketStateMapper.CreateQuoteUpdateFromTrade(normalizedUpdate), cancellationToken);

        return normalizedUpdate;
    }

    public async Task<MarketIndexUpdateDto> ApplyMarketIndexUpdateAsync(MarketIndexUpdateDto update, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var normalizedUpdate = MarketStateMapper.NormalizeIndexUpdate(update);
        var key = IndexRedisKey(normalizedUpdate.IndexName);
        var current = await GetValueAsync<MarketIndexDto>(key);
        if (current is not null && normalizedUpdate.UpdatedAt < current.UpdatedAt)
        {
            return MarketStateMapper.CreateIndexUpdateFromIndex(current);
        }

        var next = current is null
            ? MarketStateMapper.CreateIndexFromUpdate(normalizedUpdate)
            : MarketStateMapper.MergeIndex(current, normalizedUpdate);

        await _database.StringSetAsync(key, Serialize(next), EffectiveTtl());
        return normalizedUpdate;
    }

    public async Task<IReadOnlyList<MarketQuoteDto>> GetQuotesAsync(
        string boardId,
        IReadOnlyCollection<string> symbols,
        CancellationToken cancellationToken)
    {
        var normalizedBoardId = MarketStateMapper.NormalizeBoardId(boardId);
        var normalizedSymbols = MarketStateMapper.NormalizeSymbols(symbols);
        var quotes = new List<MarketQuoteDto>(normalizedSymbols.Count);

        foreach (var symbol in normalizedSymbols)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var quote = await GetValueAsync<MarketQuoteDto>(QuoteRedisKey(normalizedBoardId, symbol));
            if (quote is not null)
            {
                quotes.Add(quote);
            }
        }

        return quotes.OrderBy(quote => quote.Symbol, StringComparer.Ordinal).ToArray();
    }

    public async Task UpsertMarketIndicesAsync(IReadOnlyCollection<MarketIndexDto> indices, CancellationToken cancellationToken)
    {
        foreach (var index in indices)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var normalizedIndex = MarketStateMapper.NormalizeIndex(index);
            await _database.StringSetAsync(
                IndexRedisKey(normalizedIndex.IndexName),
                Serialize(normalizedIndex),
                EffectiveTtl());
            await _database.SetAddAsync(IndexNamesRedisKey(), normalizedIndex.IndexName);
            await _database.KeyExpireAsync(IndexNamesRedisKey(), EffectiveTtl());
        }
    }

    public async Task<IReadOnlyList<MarketIndexDto>> GetMarketIndicesAsync(
        IReadOnlyCollection<string> indexNames,
        CancellationToken cancellationToken)
    {
        var normalizedNames = MarketStateMapper.NormalizeSymbols(indexNames);
        if (normalizedNames.Count == 0)
        {
            var members = await _database.SetMembersAsync(IndexNamesRedisKey());
            normalizedNames = members
                .Select(member => MarketStateMapper.Normalize(member!))
                .Where(member => !string.IsNullOrWhiteSpace(member))
                .Distinct(StringComparer.Ordinal)
                .Order(StringComparer.Ordinal)
                .ToArray();
        }

        if (normalizedNames.Count == 0)
        {
            return [];
        }

        var indices = new List<MarketIndexDto>(normalizedNames.Count);
        foreach (var indexName in normalizedNames)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var index = await GetValueAsync<MarketIndexDto>(IndexRedisKey(indexName));
            if (index is not null)
            {
                indices.Add(index);
            }
        }

        return indices.OrderBy(index => index.IndexName, StringComparer.Ordinal).ToArray();
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
            TradeRedisKey(boardId, symbol),
            0,
            normalizedLimit - 1);

        return values
            .Select(Deserialize<MarketTradeDto>)
            .Where(trade => trade is not null)
            .Select(trade => trade!)
            .OrderByDescending(trade => trade.Time)
            .ToArray();
    }

    private async Task<T?> GetValueAsync<T>(RedisKey key)
    {
        var value = await _database.StringGetAsync(key);
        return value.HasValue ? Deserialize<T>(value) : default;
    }

    private RedisKey QuoteRedisKey(string boardId, string symbol)
    {
        return $"{KeyPrefix()}:quote:{MarketStateMapper.NormalizeBoardId(boardId)}:{MarketStateMapper.Normalize(symbol)}";
    }

    private RedisKey IndexRedisKey(string indexName)
    {
        return $"{KeyPrefix()}:index:{MarketStateMapper.Normalize(indexName)}";
    }

    private RedisKey TradeRedisKey(string boardId, string symbol)
    {
        return $"{KeyPrefix()}:trades:{MarketStateMapper.NormalizeBoardId(boardId)}:{MarketStateMapper.Normalize(symbol)}";
    }

    private RedisKey BoardSymbolsRedisKey(string boardId)
    {
        return $"{KeyPrefix()}:board-symbols:{MarketStateMapper.NormalizeBoardId(boardId)}";
    }

    private RedisKey IndexNamesRedisKey()
    {
        return $"{KeyPrefix()}:index-names";
    }

    private string KeyPrefix()
    {
        return string.IsNullOrWhiteSpace(_options.RedisKeyPrefix)
            ? "investview"
            : _options.RedisKeyPrefix.Trim().TrimEnd(':');
    }

    private TimeSpan EffectiveTtl()
    {
        return _options.LatestStateTtl <= TimeSpan.Zero ? TimeSpan.FromHours(18) : _options.LatestStateTtl;
    }

    private static RedisValue Serialize<T>(T value)
    {
        return JsonSerializer.Serialize(value, SerializerOptions);
    }

    private static T? Deserialize<T>(RedisValue value)
    {
        return value.HasValue
            ? JsonSerializer.Deserialize<T>((string)value!, SerializerOptions)
            : default;
    }
}
