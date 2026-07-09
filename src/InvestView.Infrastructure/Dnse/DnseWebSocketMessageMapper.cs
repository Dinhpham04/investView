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
            "f" => MapForeign(root),
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
        decimal? lowPrice = null)
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
            LowPrice: lowPrice);

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
            return DateTimeOffset.FromUnixTimeSeconds(unixTime);
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
