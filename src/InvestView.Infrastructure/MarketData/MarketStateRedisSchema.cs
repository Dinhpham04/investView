using System.Globalization;
using System.Text.Json;
using InvestView.Application.Dtos.MarketData;
using StackExchange.Redis;

namespace InvestView.Infrastructure.MarketData;

public sealed class MarketStateRedisSchema
{
    private const string PayloadField = "payload";
    private const string SchemaVersionField = "schemaVersion";
    private const string UpdatedAtField = "updatedAt";
    private const string ReferenceUpdatedAtField = "referenceUpdatedAt";
    private const string PriceUpdatedAtField = "priceUpdatedAt";
    private const string DepthUpdatedAtField = "depthUpdatedAt";
    private const string ForeignUpdatedAtField = "foreignUpdatedAt";
    private const string ExpectedUpdatedAtField = "expectedUpdatedAt";
    private const string StatusUpdatedAtField = "statusUpdatedAt";

    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    private readonly MarketStateOptions _options;

    public MarketStateRedisSchema(MarketStateOptions options)
    {
        _options = options;
    }

    public TimeSpan QuoteStateTtl => EffectiveTtl(_options.QuoteStateTtl, _options.LatestStateTtl, TimeSpan.FromHours(18));

    public TimeSpan SymbolDetailTtl => EffectiveTtl(_options.SymbolDetailTtl, TimeSpan.Zero, TimeSpan.FromDays(7));

    public TimeSpan LatestTradesTtl => EffectiveTtl(_options.LatestTradesTtl, TimeSpan.Zero, TimeSpan.FromDays(3));

    public TimeSpan OhlcTtl => EffectiveTtl(_options.OhlcTtl, TimeSpan.Zero, TimeSpan.FromDays(30));

    public TimeSpan MembershipTtl => EffectiveTtl(_options.MembershipTtl, TimeSpan.Zero, TimeSpan.FromDays(7));

    public TimeSpan BackfillLockTtl => EffectiveTtl(_options.BackfillLockTtl, TimeSpan.Zero, TimeSpan.FromSeconds(20));

    public RedisKey QuoteStateKey(string boardId, string symbol)
    {
        var normalizedBoardId = MarketStateMapper.NormalizeBoardId(boardId);
        var normalizedSymbol = MarketStateMapper.Normalize(symbol);
        return $"{Prefix()}:quote:{{{normalizedBoardId}:{normalizedSymbol}}}:state";
    }

    public RedisKey QuoteTradesKey(string boardId, string symbol)
    {
        var normalizedBoardId = MarketStateMapper.NormalizeBoardId(boardId);
        var normalizedSymbol = MarketStateMapper.Normalize(symbol);
        return $"{Prefix()}:quote:{{{normalizedBoardId}:{normalizedSymbol}}}:trades";
    }

    public RedisKey SymbolDetailKey(string boardId, string symbol)
    {
        var normalizedBoardId = MarketStateMapper.NormalizeBoardId(boardId);
        var normalizedSymbol = MarketStateMapper.Normalize(symbol);
        return $"{Prefix()}:security:{{{normalizedBoardId}:{normalizedSymbol}}}:detail";
    }

    public RedisKey OhlcKey(string symbol, string resolution)
    {
        return $"{Prefix()}:ohlc:{{{MarketStateMapper.Normalize(symbol)}}}:{NormalizeResolution(resolution)}";
    }

    public RedisKey OhlcCoverageKey(string symbol, string resolution)
    {
        return $"{OhlcKey(symbol, resolution)}:coverage";
    }

    public RedisKey IndexStateKey(string indexName)
    {
        var normalizedIndexName = MarketStateMapper.Normalize(indexName);
        return $"{Prefix()}:index:{{{normalizedIndexName}}}:state";
    }

    public RedisKey IndexOhlcKey(string indexName, string resolution)
    {
        return $"{Prefix()}:index:{{{MarketStateMapper.Normalize(indexName)}}}:ohlc:{NormalizeResolution(resolution)}";
    }

    public RedisKey IndexOhlcCoverageKey(string indexName, string resolution)
    {
        return $"{IndexOhlcKey(indexName, resolution)}:coverage";
    }

    public RedisKey BoardSymbolsKey(string boardId)
    {
        return $"{Prefix()}:board:{{{MarketStateMapper.NormalizeBoardId(boardId)}}}:symbols";
    }

    public RedisKey MarketSymbolsKey(string marketId)
    {
        return $"{Prefix()}:market:{{{MarketStateMapper.Normalize(marketId)}}}:symbols";
    }

    public RedisKey CategorySymbolsKey(string indexName)
    {
        return $"{Prefix()}:category:{{{MarketStateMapper.Normalize(indexName)}}}:symbols";
    }

    public RedisKey SymbolMembershipCoverageKey(RedisKey symbolsKey)
    {
        return $"{symbolsKey}:coverage";
    }

    public RedisKey IndexNamesKey()
    {
        return $"{Prefix()}:index-names";
    }

    public RedisKey SessionStateKey(string productGroupId, string boardId)
    {
        var normalizedProductGroupId = MarketStateMapper.Normalize(productGroupId);
        var normalizedBoardId = MarketStateMapper.NormalizeBoardId(boardId);
        return $"{Prefix()}:session:{{{normalizedProductGroupId}:{normalizedBoardId}}}:state";
    }

    public RedisKey BackfillLockKey(string name)
    {
        return $"{Prefix()}:locks:backfill:{MarketStateMapper.Normalize(name)}";
    }

    public HashEntry[] ToQuoteHash(MarketQuoteDto quote, bool includeGroupTimestamps)
    {
        var entries = new List<HashEntry>
        {
            Entry(PayloadField, Serialize(quote)),
            Entry(SchemaVersionField, SchemaVersion()),
            Entry("symbol", quote.Symbol),
            Entry("boardId", quote.BoardId),
            Entry("marketId", quote.MarketId),
            Entry("displayName", quote.DisplayName),
            Entry("referencePrice", quote.ReferencePrice),
            Entry("ceilingPrice", quote.CeilingPrice),
            Entry("floorPrice", quote.FloorPrice),
            Entry("lastPrice", quote.LastPrice),
            Entry("change", quote.Change),
            Entry("changePercent", quote.ChangePercent),
            Entry("lastQuantity", quote.LastQuantity),
            Entry("totalVolume", quote.TotalVolume),
            Entry("totalValue", quote.TotalValue),
            Entry("foreignBuyVolume", quote.ForeignBuyVolume),
            Entry("foreignSellVolume", quote.ForeignSellVolume),
            Entry("foreignRoom", quote.ForeignRoom),
            Entry("openPrice", quote.OpenPrice),
            Entry("highPrice", quote.HighPrice),
            Entry("lowPrice", quote.LowPrice),
            Entry("bidLevels", Serialize(quote.BidLevels)),
            Entry("askLevels", Serialize(quote.AskLevels)),
            Entry("tradingStatus", quote.TradingStatus),
            Entry(UpdatedAtField, quote.UpdatedAt)
        };
        AddIfPresent(entries, "expectedPrice", quote.ExpectedPrice);
        AddIfPresent(entries, "expectedQuantity", quote.ExpectedQuantity);

        if (includeGroupTimestamps)
        {
            entries.AddRange(AllQuoteGroupTimestampEntries(quote.UpdatedAt));
        }

        return entries.ToArray();
    }

    public HashEntry[] ToQuoteGroupTimestampHash(MarketQuoteUpdateDto update)
    {
        var entries = new List<HashEntry> { Entry(UpdatedAtField, update.UpdatedAt) };

        if (update.ReferencePrice.HasValue || update.CeilingPrice.HasValue || update.FloorPrice.HasValue ||
            update.OpenPrice.HasValue || update.HighPrice.HasValue || update.LowPrice.HasValue)
        {
            entries.Add(Entry(ReferenceUpdatedAtField, update.UpdatedAt));
        }

        if (update.LastPrice.HasValue || update.Change.HasValue || update.ChangePercent.HasValue ||
            update.LastQuantity.HasValue || update.TotalVolume.HasValue || update.TotalValue.HasValue)
        {
            entries.Add(Entry(PriceUpdatedAtField, update.UpdatedAt));
        }

        if (update.BidLevels is not null || update.AskLevels is not null)
        {
            entries.Add(Entry(DepthUpdatedAtField, update.UpdatedAt));
        }

        if (update.ForeignBuyVolume.HasValue || update.ForeignSellVolume.HasValue || update.ForeignRoom.HasValue)
        {
            entries.Add(Entry(ForeignUpdatedAtField, update.UpdatedAt));
        }

        if (update.ExpectedPrice.HasValue || update.ExpectedQuantity.HasValue)
        {
            entries.Add(Entry(ExpectedUpdatedAtField, update.UpdatedAt));
        }

        if (!string.IsNullOrWhiteSpace(update.TradingStatus))
        {
            entries.Add(Entry(StatusUpdatedAtField, update.UpdatedAt));
        }

        return entries.ToArray();
    }

    public MarketQuoteDto? QuoteFromHash(HashEntry[] entries)
    {
        return FromHashPayload<MarketQuoteDto>(entries);
    }

    public HashEntry[] ToSymbolDetailHash(SymbolDetailDto detail)
    {
        return
        [
            Entry(PayloadField, Serialize(detail)),
            Entry(SchemaVersionField, SchemaVersion()),
            Entry("symbol", detail.Symbol),
            Entry("boardId", detail.BoardId),
            Entry("marketId", detail.MarketId),
            Entry("displayName", detail.DisplayName),
            Entry("name", detail.Name),
            Entry("securityType", detail.SecurityType),
            Entry("isin", detail.Isin),
            Entry("referencePrice", detail.ReferencePrice),
            Entry("lastPrice", detail.LastPrice),
            Entry("foreignRoom", detail.ForeignRoom),
            Entry(UpdatedAtField, detail.UpdatedAt)
        ];
    }

    public SymbolDetailDto? SymbolDetailFromHash(HashEntry[] entries)
    {
        return FromHashPayload<SymbolDetailDto>(entries);
    }

    public HashEntry[] ToIndexHash(MarketIndexDto index)
    {
        var entries = new List<HashEntry>
        {
            Entry(PayloadField, Serialize(index)),
            Entry(SchemaVersionField, SchemaVersion()),
            Entry("indexName", index.IndexName),
            Entry("marketId", index.MarketId),
            Entry("tradingSessionId", index.TradingSessionId),
            Entry(UpdatedAtField, index.UpdatedAt)
        };

        AddIfPresent(entries, "value", index.Value);
        AddIfPresent(entries, "change", index.Change);
        AddIfPresent(entries, "changePercent", index.ChangePercent);
        AddIfPresent(entries, "referenceValue", index.ReferenceValue);
        AddIfPresent(entries, "highValue", index.HighValue);
        AddIfPresent(entries, "lowValue", index.LowValue);
        AddIfPresent(entries, "totalVolume", index.TotalVolume);
        AddIfPresent(entries, "totalValue", index.TotalValue);
        AddIfPresent(entries, "estimatedValue", index.EstimatedValue);
        AddIfPresent(entries, "estimatedChange", index.EstimatedChange);
        AddIfPresent(entries, "estimatedChangePercent", index.EstimatedChangePercent);
        AddIfPresent(entries, "estimatedTotalVolume", index.EstimatedTotalVolume);
        AddIfPresent(entries, "estimatedTotalValue", index.EstimatedTotalValue);
        if (index.EstimatedUpdatedAt.HasValue)
        {
            entries.Add(Entry("estimatedUpdatedAt", index.EstimatedUpdatedAt.Value));
        }

        return entries.ToArray();
    }

    public MarketIndexDto? IndexFromHash(HashEntry[] entries)
    {
        return FromHashPayload<MarketIndexDto>(entries);
    }

    public HashEntry[] ToSessionHash(MarketSessionUpdateDto session)
    {
        return
        [
            Entry(PayloadField, Serialize(session)),
            Entry(SchemaVersionField, SchemaVersion()),
            Entry("marketId", session.MarketId),
            Entry("boardId", session.BoardId),
            Entry("productGroupId", session.ProductGroupId),
            Entry("eventId", session.EventId),
            Entry("tradingSessionId", session.TradingSessionId),
            Entry(UpdatedAtField, session.UpdatedAt)
        ];
    }

    public MarketSessionUpdateDto? SessionFromHash(HashEntry[] entries)
    {
        return FromHashPayload<MarketSessionUpdateDto>(entries);
    }

    public RedisValue ToOhlcMember(OhlcBarDto bar)
    {
        return Serialize(bar);
    }

    public OhlcBarDto? OhlcFromMember(RedisValue value)
    {
        return value.HasValue ? Deserialize<OhlcBarDto>((string)value!) : null;
    }

    public RedisValue ToTradeMember(MarketTradeDto trade)
    {
        return Serialize(trade);
    }

    public MarketTradeDto? TradeFromMember(RedisValue value)
    {
        return value.HasValue ? Deserialize<MarketTradeDto>((string)value!) : null;
    }

    public double Score(DateTimeOffset time)
    {
        return time.ToUnixTimeMilliseconds();
    }

    public double MinScore(DateTimeOffset? from)
    {
        return from?.ToUnixTimeMilliseconds() ?? double.NegativeInfinity;
    }

    public double MaxScore(DateTimeOffset? to)
    {
        return to?.ToUnixTimeMilliseconds() ?? double.PositiveInfinity;
    }

    public RedisValue CoverageToken(DateTimeOffset? from, DateTimeOffset? to)
    {
        return $"{from?.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture) ?? string.Empty}:{to?.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture) ?? string.Empty}";
    }

    private IEnumerable<HashEntry> AllQuoteGroupTimestampEntries(DateTimeOffset updatedAt)
    {
        yield return Entry(ReferenceUpdatedAtField, updatedAt);
        yield return Entry(PriceUpdatedAtField, updatedAt);
        yield return Entry(DepthUpdatedAtField, updatedAt);
        yield return Entry(ForeignUpdatedAtField, updatedAt);
        yield return Entry(ExpectedUpdatedAtField, updatedAt);
        yield return Entry(StatusUpdatedAtField, updatedAt);
    }

    private T? FromHashPayload<T>(HashEntry[] entries)
    {
        var payload = entries.FirstOrDefault(entry => entry.Name == PayloadField).Value;
        return payload.HasValue ? Deserialize<T>((string)payload!) : default;
    }

    private string Prefix()
    {
        var prefix = CleanSegment(_options.RedisKeyPrefix, "investview");
        var environment = CleanSegment(_options.RedisEnvironment, "dev");
        return $"{prefix}:{environment}:md:{SchemaVersion()}";
    }

    private string SchemaVersion()
    {
        return CleanSegment(_options.RedisSchemaVersion, "v1");
    }

    private static string NormalizeResolution(string resolution)
    {
        return string.IsNullOrWhiteSpace(resolution) ? "1" : resolution.Trim().ToUpperInvariant();
    }

    private static string CleanSegment(string value, string fallback)
    {
        return string.IsNullOrWhiteSpace(value)
            ? fallback
            : value.Trim().Trim(':').ToLowerInvariant();
    }

    private static TimeSpan EffectiveTtl(TimeSpan preferred, TimeSpan fallback, TimeSpan defaultValue)
    {
        if (preferred > TimeSpan.Zero)
        {
            return preferred;
        }

        return fallback > TimeSpan.Zero ? fallback : defaultValue;
    }

    private static void AddIfPresent(List<HashEntry> entries, string name, decimal? value)
    {
        if (value.HasValue)
        {
            entries.Add(Entry(name, value.Value));
        }
    }

    private static void AddIfPresent(List<HashEntry> entries, string name, long? value)
    {
        if (value.HasValue)
        {
            entries.Add(Entry(name, value.Value));
        }
    }

    private static HashEntry Entry(string name, string value)
    {
        return new HashEntry(name, value);
    }

    private static HashEntry Entry(string name, decimal value)
    {
        return new HashEntry(name, value.ToString(CultureInfo.InvariantCulture));
    }

    private static HashEntry Entry(string name, long value)
    {
        return new HashEntry(name, value.ToString(CultureInfo.InvariantCulture));
    }

    private static HashEntry Entry(string name, DateTimeOffset value)
    {
        return new HashEntry(name, value.ToString("O", CultureInfo.InvariantCulture));
    }

    private static string Serialize<T>(T value)
    {
        return JsonSerializer.Serialize(value, SerializerOptions);
    }

    private static T? Deserialize<T>(string value)
    {
        return JsonSerializer.Deserialize<T>(value, SerializerOptions);
    }
}
