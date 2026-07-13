using System.Text.Json;
using InvestView.Application.Dtos.MarketData;
using Microsoft.Extensions.Options;

namespace InvestView.Infrastructure.Dnse;

public sealed class DnseWebSocketMessageMapper
{
    private const decimal PriceScaleFactor = 1000m;
    private readonly TimeProvider _timeProvider;
    private readonly int _quantityScaleFactor;

    public DnseWebSocketMessageMapper(TimeProvider timeProvider)
        : this(Options.Create(new DnseMarketDataOptions()), timeProvider)
    {
    }

    public DnseWebSocketMessageMapper(IOptions<DnseMarketDataOptions> options, TimeProvider timeProvider)
    {
        _timeProvider = timeProvider;
        _quantityScaleFactor = Math.Max(options.Value.QuantityScaleFactor, 1);
    }

    public DnseWebSocketMessage Map(string json)
    {
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;

        if (TryGetString(root, "action", out var action))
        {
            return MapControlMessage(root, action);
        }

        if (!TryGetString(root, "T", out var messageType))
        {
            return new DnseWebSocketMessage(DnseWebSocketMessageKind.Unknown);
        }

        return messageType switch
        {
            "sd" => MapSecurityDefinition(root),
            "t" => MapTrade(root),
            "te" => MapTradeExtra(root),
            "q" => MapTopPrice(root),
            "b" => MapOhlc(root, isClosed: false),
            "bc" => MapOhlc(root, isClosed: true),
            "e" => MapExpectedPrice(root),
            "f" => MapForeign(root),
            "mi" => MapMarketIndex(root),
            "emi" => MapEstimatedMarketIndex(root),
            "s" => MapSession(root),
            _ => new DnseWebSocketMessage(DnseWebSocketMessageKind.Unknown)
        };
    }

    private static DnseWebSocketMessage MapControlMessage(JsonElement root, string action)
    {
        return action switch
        {
            "ping" => new DnseWebSocketMessage(DnseWebSocketMessageKind.Ping, Action: action),
            "pong" => new DnseWebSocketMessage(DnseWebSocketMessageKind.Pong, Action: action),
            "auth_success" => new DnseWebSocketMessage(DnseWebSocketMessageKind.AuthSuccess, Action: action),
            "subscribed" => new DnseWebSocketMessage(DnseWebSocketMessageKind.Subscribed, Action: action),
            "auth_error" or "error" => new DnseWebSocketMessage(
                DnseWebSocketMessageKind.Error,
                ErrorMessage: GetOptionalString(root, "message") ?? GetOptionalString(root, "msg") ?? "DNSE websocket error.",
                Action: action),
            _ => new DnseWebSocketMessage(DnseWebSocketMessageKind.Unknown, Action: action)
        };
    }

    private DnseWebSocketMessage MapSecurityDefinition(JsonElement root)
    {
        return QuoteUpdate(
            root,
            referencePrice: NormalizePrice(GetOptionalDecimal(root, "basicPrice")),
            ceilingPrice: NormalizePrice(GetOptionalDecimal(root, "ceilingPrice")),
            floorPrice: NormalizePrice(GetOptionalDecimal(root, "floorPrice")),
            tradingStatus: GetOptionalString(root, "securityStatus"));
    }

    private DnseWebSocketMessage MapTrade(JsonElement root)
    {
        return QuoteUpdate(
            root,
            lastPrice: NormalizePrice(GetOptionalDecimal(root, "matchPrice")),
            lastQuantity: ScaleQuantity(GetOptionalLong(root, "matchQtty")),
            totalVolume: ScaleQuantity(GetOptionalLong(root, "totalVolumeTraded")),
            totalValue: GetOptionalDecimal(root, "grossTradeAmount"),
            openPrice: NormalizePrice(GetOptionalDecimal(root, "openPrice")),
            highPrice: NormalizePrice(GetOptionalDecimal(root, "highestPrice")),
            lowPrice: NormalizePrice(GetOptionalDecimal(root, "lowestPrice")),
            tradingStatus: GetOptionalString(root, "tradingSessionId"));
    }

    private DnseWebSocketMessage MapTradeExtra(JsonElement root)
    {
        if (!TryGetString(root, "symbol", out var symbol))
        {
            return new DnseWebSocketMessage(DnseWebSocketMessageKind.Unknown);
        }

        var boardId = GetOptionalString(root, "boardId") ?? "G1";
        var updatedAt = GetOptionalTimestamp(root, "time")
            ?? GetOptionalTimestamp(root, "transactTime")
            ?? _timeProvider.GetUtcNow();
        var update = new MarketTradeUpdateDto(
            Symbol: symbol.Trim().ToUpperInvariant(),
            BoardId: boardId.Trim().ToUpperInvariant(),
            Time: updatedAt,
            Price: NormalizePrice(GetOptionalDecimal(root, "matchPrice")),
            Change: null,
            ChangePercent: null,
            Quantity: ScaleQuantity(GetOptionalLong(root, "matchQtty")),
            TotalVolume: ScaleQuantity(GetOptionalLong(root, "totalVolumeTraded")),
            TotalValue: GetOptionalDecimal(root, "grossTradeAmount"),
            Side: NormalizeTradeSide(GetOptionalString(root, "side")));

        return new DnseWebSocketMessage(DnseWebSocketMessageKind.TradeUpdate, TradeUpdate: update);
    }

    private DnseWebSocketMessage MapTopPrice(JsonElement root)
    {
        return QuoteUpdate(
            root,
            bidLevels: ReadPriceLevels(root, "bid"),
            askLevels: ReadPriceLevels(root, "offer"));
    }

    private DnseWebSocketMessage MapOhlc(JsonElement root, bool isClosed)
    {
        if (!TryGetString(root, "symbol", out var symbol))
        {
            return new DnseWebSocketMessage(DnseWebSocketMessageKind.Unknown);
        }

        var resolution = GetOptionalString(root, "resolution") ?? "1";
        var type = NormalizeMessageType(GetOptionalString(root, "type") ?? "STOCK");
        var isIndex = type.Equals("INDEX", StringComparison.Ordinal);
        var barTime = GetOptionalTimestamp(root, "time") ?? _timeProvider.GetUtcNow();
        var updatedAt = GetOptionalTimestamp(root, "lastUpdated") ?? barTime;
        var update = new MarketOhlcUpdateDto(
            Symbol: symbol.Trim().ToUpperInvariant(),
            Resolution: resolution.Trim().ToUpperInvariant(),
            Time: barTime,
            Open: NormalizeOhlcPrice(GetOptionalDecimal(root, "open"), isIndex) ?? 0m,
            High: NormalizeOhlcPrice(GetOptionalDecimal(root, "high"), isIndex) ?? 0m,
            Low: NormalizeOhlcPrice(GetOptionalDecimal(root, "low"), isIndex) ?? 0m,
            Close: NormalizeOhlcPrice(GetOptionalDecimal(root, "close"), isIndex) ?? 0m,
            Volume: ScaleQuantity(GetOptionalLong(root, "volume")) ?? 0,
            Type: type,
            IsClosed: isClosed,
            UpdatedAt: updatedAt);

        return new DnseWebSocketMessage(DnseWebSocketMessageKind.OhlcUpdate, OhlcUpdate: update);
    }

    private DnseWebSocketMessage MapExpectedPrice(JsonElement root)
    {
        return QuoteUpdate(
            root,
            expectedPrice: NormalizePrice(GetOptionalDecimal(root, "expectedTradePrice")),
            expectedQuantity: ScaleQuantity(GetOptionalLong(root, "expectedTradeQuantity")));
    }

    private DnseWebSocketMessage MapForeign(JsonElement root)
    {
        return QuoteUpdate(
            root,
            foreignBuyVolume: GetOptionalLong(root, "totalBuyVolume") ?? GetOptionalLong(root, "buyVolume"),
            foreignSellVolume: GetOptionalLong(root, "totalSellVolume") ?? GetOptionalLong(root, "sellVolume"),
            foreignRoom: GetOptionalLong(root, "foreignerBuyPossibleQuantity")
                ?? GetOptionalLong(root, "currentRoom")
                ?? GetOptionalLong(root, "foreignBuyRoom"));
    }

    private DnseWebSocketMessage MapMarketIndex(JsonElement root)
    {
        if (!TryGetString(root, "indexName", out var indexName))
        {
            return new DnseWebSocketMessage(DnseWebSocketMessageKind.Unknown);
        }

        var updatedAt = GetOptionalTimestamp(root, "transactTime")
            ?? GetOptionalTimestamp(root, "time")
            ?? _timeProvider.GetUtcNow();
        var update = new MarketIndexUpdateDto(
            IndexName: indexName.Trim().ToUpperInvariant(),
            Value: GetOptionalDecimal(root, "valueIndexes"),
            Change: GetOptionalDecimal(root, "changedValue"),
            ChangePercent: GetOptionalDecimal(root, "changedRatio"),
            ReferenceValue: GetOptionalDecimal(root, "priorValueIndexes"),
            HighValue: GetOptionalDecimal(root, "highestValueIndexes"),
            LowValue: GetOptionalDecimal(root, "lowestValueIndexes"),
            TotalVolume: GetOptionalLong(root, "totalVolumeTraded"),
            TotalValue: GetMarketIndexTotalValue(root),
            UpCount: GetOptionalInt(root, "fluctuationUpIssueCount"),
            DownCount: GetOptionalInt(root, "fluctuationDownIssueCount"),
            NoChangeCount: GetOptionalInt(root, "fluctuationSteadinessIssueCount"),
            CeilingCount: GetOptionalInt(root, "fluctuationUpperLimitIssueCount"),
            FloorCount: GetOptionalInt(root, "fluctuationLowerLimitIssueCount"),
            MarketId: GetOptionalString(root, "marketId") ?? string.Empty,
            TradingSessionId: GetOptionalString(root, "tradingSessionId") ?? string.Empty,
            UpdatedAt: updatedAt);

        return new DnseWebSocketMessage(DnseWebSocketMessageKind.MarketIndexUpdate, MarketIndexUpdate: update);
    }

    private DnseWebSocketMessage MapEstimatedMarketIndex(JsonElement root)
    {
        if (!TryGetString(root, "indexName", out var indexName))
        {
            return new DnseWebSocketMessage(DnseWebSocketMessageKind.Unknown);
        }

        var updatedAt = GetOptionalTimestamp(root, "time")
            ?? GetOptionalTimestamp(root, "transactTime")
            ?? _timeProvider.GetUtcNow();
        var update = new MarketIndexUpdateDto(
            IndexName: indexName.Trim().ToUpperInvariant(),
            Value: null,
            Change: null,
            ChangePercent: null,
            ReferenceValue: null,
            HighValue: null,
            LowValue: null,
            TotalVolume: null,
            TotalValue: null,
            UpCount: null,
            DownCount: null,
            NoChangeCount: null,
            CeilingCount: null,
            FloorCount: null,
            MarketId: string.Empty,
            TradingSessionId: string.Empty,
            UpdatedAt: updatedAt,
            EstimatedValue: GetOptionalDecimal(root, "valueIndexes"),
            EstimatedChange: GetOptionalDecimal(root, "changedValue"),
            EstimatedChangePercent: GetOptionalDecimal(root, "changedRatio"),
            EstimatedTotalVolume: GetOptionalLong(root, "totalVolumeTraded"),
            EstimatedTotalValue: GetMarketIndexTotalValue(root),
            EstimatedUpdatedAt: updatedAt);

        return new DnseWebSocketMessage(DnseWebSocketMessageKind.MarketIndexUpdate, MarketIndexUpdate: update);
    }

    private DnseWebSocketMessage MapSession(JsonElement root)
    {
        var updatedAt = GetOptionalTimestamp(root, "time")
            ?? GetOptionalTimestamp(root, "sendingTime")
            ?? _timeProvider.GetUtcNow();
        var update = new MarketSessionUpdateDto(
            MarketId: GetOptionalString(root, "marketId") ?? string.Empty,
            BoardId: GetOptionalString(root, "boardId") ?? "G1",
            ProductGroupId: GetOptionalString(root, "tscProdGrpId") ?? GetOptionalString(root, "productGroupId") ?? "STO",
            EventId: GetOptionalString(root, "eventId") ?? string.Empty,
            TradingSessionId: GetOptionalString(root, "tradingSessionId") ?? string.Empty,
            UpdatedAt: updatedAt);

        return new DnseWebSocketMessage(DnseWebSocketMessageKind.MarketSessionUpdate, MarketSessionUpdate: update);
    }

    private DnseWebSocketMessage QuoteUpdate(
        JsonElement root,
        decimal? lastPrice = null,
        long? lastQuantity = null,
        long? totalVolume = null,
        decimal? totalValue = null,
        long? foreignBuyVolume = null,
        long? foreignSellVolume = null,
        long? foreignRoom = null,
        IReadOnlyList<PriceLevelDto>? bidLevels = null,
        IReadOnlyList<PriceLevelDto>? askLevels = null,
        string? tradingStatus = null,
        decimal? referencePrice = null,
        decimal? ceilingPrice = null,
        decimal? floorPrice = null,
        decimal? openPrice = null,
        decimal? highPrice = null,
        decimal? lowPrice = null,
        decimal? expectedPrice = null,
        long? expectedQuantity = null)
    {
        if (!TryGetString(root, "symbol", out var symbol))
        {
            return new DnseWebSocketMessage(DnseWebSocketMessageKind.Unknown);
        }

        var boardId = GetOptionalString(root, "boardId") ?? "G1";
        var updatedAt = GetOptionalTimestamp(root, "time")
            ?? GetOptionalTimestamp(root, "transactTime")
            ?? _timeProvider.GetUtcNow();

        var update = new MarketQuoteUpdateDto(
            Symbol: symbol.Trim().ToUpperInvariant(),
            BoardId: boardId.Trim().ToUpperInvariant(),
            LastPrice: lastPrice,
            Change: null,
            ChangePercent: null,
            LastQuantity: lastQuantity,
            TotalVolume: totalVolume,
            TotalValue: totalValue,
            ForeignBuyVolume: foreignBuyVolume,
            ForeignSellVolume: foreignSellVolume,
            ForeignRoom: foreignRoom,
            BidLevels: bidLevels,
            AskLevels: askLevels,
            TradingStatus: tradingStatus,
            UpdatedAt: updatedAt,
            ReferencePrice: referencePrice,
            CeilingPrice: ceilingPrice,
            FloorPrice: floorPrice,
            OpenPrice: openPrice,
            HighPrice: highPrice,
            LowPrice: lowPrice,
            ExpectedPrice: expectedPrice,
            ExpectedQuantity: expectedQuantity);

        return new DnseWebSocketMessage(DnseWebSocketMessageKind.QuoteUpdate, update);
    }

    private IReadOnlyList<PriceLevelDto>? ReadPriceLevels(JsonElement root, string propertyName)
    {
        if (!root.TryGetProperty(propertyName, out var levelsElement) || levelsElement.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        return levelsElement
            .EnumerateArray()
            .Select(level => new PriceLevelDto(
                NormalizePrice(GetOptionalDecimal(level, "price")) ?? 0m,
                ScaleQuantity(GetOptionalLong(level, "quantity") ?? GetOptionalLong(level, "qtty")) ?? 0))
            .ToArray();
    }

    private static decimal? NormalizePrice(decimal? value)
    {
        if (value is null or <= 0m || Math.Abs(value.Value) >= PriceScaleFactor)
        {
            return value;
        }

        return value.Value * PriceScaleFactor;
    }

    private static decimal? NormalizeOhlcPrice(decimal? value, bool isIndex)
    {
        return isIndex ? value : NormalizePrice(value);
    }

    private static string NormalizeMessageType(string value)
    {
        return string.IsNullOrWhiteSpace(value) ? "STOCK" : value.Trim().ToUpperInvariant();
    }

    private static decimal? GetMarketIndexTotalValue(JsonElement root)
    {
        var grossTradeAmount = GetOptionalDecimal(root, "grossTradeAmount");
        if (grossTradeAmount is > 0m)
        {
            return grossTradeAmount;
        }

        var continuousAuctionValue = GetOptionalDecimal(root, "contauctAccTrdVal");
        var blockTradeValue = GetOptionalDecimal(root, "blkTrdAccTrdVal");
        if (continuousAuctionValue is not null || blockTradeValue is not null)
        {
            return (continuousAuctionValue ?? 0m) + (blockTradeValue ?? 0m);
        }

        return grossTradeAmount;
    }

    private long? ScaleQuantity(long? value)
    {
        if (value is null or <= 0 || _quantityScaleFactor <= 1)
        {
            return value;
        }

        return checked(value.Value * _quantityScaleFactor);
    }

    private static string? GetOptionalString(JsonElement element, string propertyName)
    {
        return TryGetString(element, propertyName, out var value) ? value : null;
    }

    private static bool TryGetString(JsonElement element, string propertyName, out string value)
    {
        value = string.Empty;

        if (!element.TryGetProperty(propertyName, out var property))
        {
            return false;
        }

        if (property.ValueKind == JsonValueKind.String)
        {
            value = property.GetString() ?? string.Empty;
            return true;
        }

        if (property.ValueKind == JsonValueKind.Number)
        {
            value = property.GetRawText();
            return true;
        }

        return false;
    }

    private static decimal? GetOptionalDecimal(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var property))
        {
            return null;
        }

        if (property.ValueKind == JsonValueKind.Number && property.TryGetDecimal(out var value))
        {
            return value;
        }

        if (property.ValueKind == JsonValueKind.String && decimal.TryParse(property.GetString(), out value))
        {
            return value;
        }

        return null;
    }

    private static long? GetOptionalLong(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var property))
        {
            return null;
        }

        if (property.ValueKind == JsonValueKind.Number)
        {
            return property.TryGetInt64(out var longValue)
                ? longValue
                : (long?)decimal.ToInt64(property.GetDecimal());
        }

        return property.ValueKind == JsonValueKind.String && decimal.TryParse(property.GetString(), out var decimalValue)
            ? decimal.ToInt64(decimalValue)
            : null;
    }

    private static int? GetOptionalInt(JsonElement element, string propertyName)
    {
        var value = GetOptionalLong(element, propertyName);
        return value is null ? null : checked((int)value.Value);
    }

    private static DateTimeOffset? GetOptionalTimestamp(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var property))
        {
            return null;
        }

        if (property.ValueKind == JsonValueKind.Object)
        {
            var seconds = GetOptionalLong(property, "Seconds") ?? GetOptionalLong(property, "seconds");
            var nanos = GetOptionalLong(property, "Nanos") ?? GetOptionalLong(property, "nanos") ?? 0;

            return seconds is null
                ? null
                : DateTimeOffset.FromUnixTimeSeconds(seconds.Value).AddTicks(nanos / 100);
        }

        if (property.ValueKind == JsonValueKind.Number && property.TryGetInt64(out var unixTime))
        {
            return unixTime > 1_000_000_000_000
                ? DateTimeOffset.FromUnixTimeMilliseconds(unixTime)
                : DateTimeOffset.FromUnixTimeSeconds(unixTime);
        }

        return property.ValueKind == JsonValueKind.String && DateTimeOffset.TryParse(property.GetString(), out var timestamp)
            ? timestamp
            : null;
    }

    private static string NormalizeTradeSide(string? side)
    {
        if (string.IsNullOrWhiteSpace(side))
        {
            return string.Empty;
        }

        var normalized = side.Trim().ToUpperInvariant();
        return normalized switch
        {
            "BUY" or "B" or "1" => "B",
            "SELL" or "S" or "2" => "S",
            _ => normalized
        };
    }
}
