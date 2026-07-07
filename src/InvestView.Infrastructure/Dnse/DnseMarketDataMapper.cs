using System.Text.Json;
using InvestView.Application.Dtos.MarketData;

namespace InvestView.Infrastructure.Dnse;

public static class DnseMarketDataMapper
{
    public static MarketQuoteDto MapMarketQuote(
        string symbol,
        string boardId,
        JsonElement? instrument,
        JsonElement? securityDefinition,
        JsonElement? latestTrade,
        JsonElement? latestQuote,
        JsonElement? foreignTrading,
        DateTimeOffset fallbackUpdatedAt,
        int quantityScaleFactor = 1)
    {
        var normalizedSymbol = NormalizeSymbol(symbol);
        var normalizedBoardId = NormalizeToken(boardId, "G1");
        var instrumentPayload = FirstObjectOrSelf(UnwrapPayload(instrument, "data", "instruments", "instrument"));
        var secdefPayload = FirstObjectOrSelf(UnwrapPayload(securityDefinition, "data", "secdef", "secdefs", "securityDefinition", "securityDefinitions"));
        var tradePayload = FirstObjectOrSelf(UnwrapPayload(latestTrade, "data", "trade", "trades", "ticks"));
        var quotePayload = FirstObjectOrSelf(UnwrapPayload(latestQuote, "data", "quote", "quotes", "topPrice", "topPrices"));
        var foreignPayload = SelectForeignTradingPayload(
            UnwrapPayload(foreignTrading, "data", "foreigners", "foreigner", "foreignTradings"),
            normalizedSymbol,
            normalizedBoardId);

        var normalizedQuantityScaleFactor = Math.Max(quantityScaleFactor, 1);
        var referencePrice = GetDecimal(secdefPayload, "referencePrice", "refPrice", "basicPrice", "priorClosePrice");
        var ceilingPrice = GetDecimal(secdefPayload, "ceilingPrice", "ceilPrice", "ceiling");
        var floorPrice = GetDecimal(secdefPayload, "floorPrice", "floor");
        var lastPrice = GetDecimal(tradePayload, "lastPrice", "matchPrice", "price", "closePrice");
        var openPrice = GetDecimal(tradePayload, "openPrice", "open");
        var highPrice = GetDecimal(tradePayload, "highestPrice", "highPrice", "high");
        var lowPrice = GetDecimal(tradePayload, "lowestPrice", "lowPrice", "low");
        var bidLevels = GetPriceLevels(quotePayload, ["bid", "bids", "bidLevels", "buy", "buyLevels"], "bid");
        var askLevels = GetPriceLevels(quotePayload, ["offer", "offers", "offerLevels", "ask", "asks", "askLevels", "sell", "sellLevels"], "ask");
        var priceScaleAnchor = GetPriceScaleAnchor(
            [referencePrice, ceilingPrice, floorPrice, lastPrice, openPrice, highPrice, lowPrice],
            bidLevels,
            askLevels);

        referencePrice = NormalizePriceScale(referencePrice, priceScaleAnchor);
        ceilingPrice = NormalizePriceScale(ceilingPrice, priceScaleAnchor);
        floorPrice = NormalizePriceScale(floorPrice, priceScaleAnchor);
        lastPrice = NormalizePriceScale(lastPrice, priceScaleAnchor);
        openPrice = NormalizePriceScale(openPrice, priceScaleAnchor);
        highPrice = NormalizePriceScale(highPrice, priceScaleAnchor);
        lowPrice = NormalizePriceScale(lowPrice, priceScaleAnchor);
        bidLevels = NormalizePriceLevels(bidLevels, priceScaleAnchor, normalizedQuantityScaleFactor);
        askLevels = NormalizePriceLevels(askLevels, priceScaleAnchor, normalizedQuantityScaleFactor);

        var change = GetDecimal(tradePayload, "change", "changedValue", "priceChange");
        change = NormalizePriceDeltaScale(change, priceScaleAnchor);
        if (change == 0m && referencePrice > 0m && lastPrice > 0m)
        {
            change = lastPrice - referencePrice;
        }

        var changePercent = GetDecimal(tradePayload, "changePercent", "changedPercent", "priceChangePercent");
        if (changePercent == 0m && referencePrice > 0m && change != 0m)
        {
            changePercent = Math.Round(change / referencePrice * 100m, 2, MidpointRounding.AwayFromZero);
        }

        var totalVolume = ScaleQuantity(
            GetLong(tradePayload, "totalVolumeTraded", "totalVolume", "accumulatedVolume", "volume"),
            normalizedQuantityScaleFactor);
        var totalValue = GetDecimal(tradePayload, "grossTradeAmount", "totalValue", "accumulatedValue", "value");
        if (totalValue == 0m && lastPrice > 0m && totalVolume > 0)
        {
            totalValue = lastPrice * totalVolume;
        }

        var updatedAt =
            GetDateTimeOffset(tradePayload, "updatedAt", "time", "timestamp", "tradingTime")
            ?? GetDateTimeOffset(quotePayload, "updatedAt", "time", "timestamp", "tradingTime")
            ?? fallbackUpdatedAt;

        return new MarketQuoteDto(
            Symbol: normalizedSymbol,
            BoardId: normalizedBoardId,
            MarketId: GetString(instrumentPayload, "marketId", "exchange", "exchangeId") ?? "UNKNOWN",
            DisplayName: GetString(instrumentPayload, "displayName", "name", "symbolName", "organShortName") ?? normalizedSymbol,
            ReferencePrice: referencePrice,
            CeilingPrice: ceilingPrice,
            FloorPrice: floorPrice,
            LastPrice: lastPrice,
            Change: change,
            ChangePercent: changePercent,
            LastQuantity: ScaleQuantity(
                GetLong(tradePayload, "lastQuantity", "matchQtty", "matchQuantity", "quantity", "qtty"),
                normalizedQuantityScaleFactor),
            TotalVolume: totalVolume,
            TotalValue: totalValue,
            ForeignBuyVolume: GetLong(foreignPayload, "foreignBuyVolume", "totalBuyVolume", "buyVolume", "buyQtty"),
            ForeignSellVolume: GetLong(foreignPayload, "foreignSellVolume", "totalSellVolume", "sellVolume", "sellQtty"),
            ForeignRoom: GetLong(foreignPayload, "foreignRoom", "foreignerBuyPossibleQuantity", "foreignerOrderLimitQuantity", "room", "currentRoom"),
            OpenPrice: openPrice > 0m ? openPrice : lastPrice,
            HighPrice: highPrice > 0m ? highPrice : lastPrice,
            LowPrice: lowPrice > 0m ? lowPrice : lastPrice,
            BidLevels: bidLevels,
            AskLevels: askLevels,
            TradingStatus: GetString(secdefPayload, "tradingStatus", "status", "securityStatus") ?? "Unknown",
            UpdatedAt: updatedAt);
    }

    public static SymbolDetailDto MapSymbolDetail(
        string symbol,
        string boardId,
        JsonElement? instrument,
        JsonElement? securityDefinition,
        DateTimeOffset fallbackUpdatedAt)
    {
        var normalizedSymbol = NormalizeSymbol(symbol);
        var instrumentPayload = FirstObjectOrSelf(UnwrapPayload(instrument, "data", "instruments", "instrument"));
        var secdefPayload = FirstObjectOrSelf(UnwrapPayload(securityDefinition, "data", "secdef", "secdefs", "securityDefinition", "securityDefinitions"));

        return new SymbolDetailDto(
            Symbol: normalizedSymbol,
            BoardId: NormalizeToken(boardId, "G1"),
            MarketId: GetString(instrumentPayload, "marketId", "exchange", "exchangeId") ?? "UNKNOWN",
            DisplayName: GetString(instrumentPayload, "displayName", "name", "symbolName", "organShortName") ?? normalizedSymbol,
            Name: GetString(instrumentPayload, "fullName", "name", "symbolName", "organName") ?? normalizedSymbol,
            SecurityType: GetString(instrumentPayload, "securityType", "type", "securityGroupId") ?? "Stock",
            ReferencePrice: GetDecimal(secdefPayload, "referencePrice", "refPrice", "basicPrice", "priorClosePrice"),
            CeilingPrice: GetDecimal(secdefPayload, "ceilingPrice", "ceilPrice", "ceiling"),
            FloorPrice: GetDecimal(secdefPayload, "floorPrice", "floor"),
            TradingStatus: GetString(secdefPayload, "tradingStatus", "status", "securityStatus") ?? "Unknown",
            UpdatedAt: GetDateTimeOffset(secdefPayload, "updatedAt", "time", "timestamp") ?? fallbackUpdatedAt);
    }

    public static JsonElement? FindObjectBySymbol(JsonElement root, string symbol)
    {
        var payload = UnwrapPayload(root, "data", "instruments", "instrument");
        if (payload.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in payload.EnumerateArray())
            {
                if (MatchesSymbol(item, symbol))
                {
                    return item;
                }
            }

            return null;
        }

        return payload.ValueKind == JsonValueKind.Object ? payload : null;
    }

    public static IReadOnlyList<string> ExtractInstrumentSymbols(JsonElement root, int limit)
    {
        var payload = UnwrapPayload(root, "data", "instruments", "instrument");
        var symbols = new List<string>();

        if (payload.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in payload.EnumerateArray())
            {
                var symbol = NormalizeToken(GetString(item, "symbol", "code", "ticker") ?? string.Empty, string.Empty);
                if (!string.IsNullOrWhiteSpace(symbol) &&
                    !symbols.Contains(symbol, StringComparer.Ordinal) &&
                    symbols.Count < limit)
                {
                    symbols.Add(symbol);
                }
            }

            return symbols;
        }

        if (payload.ValueKind == JsonValueKind.Object)
        {
            var symbol = NormalizeToken(GetString(payload, "symbol", "code", "ticker") ?? string.Empty, string.Empty);
            if (!string.IsNullOrWhiteSpace(symbol))
            {
                symbols.Add(symbol);
            }
        }

        return symbols;
    }

    public static IReadOnlyList<string> ExtractInstrumentPayloads(JsonElement root)
    {
        var payload = UnwrapPayload(root, "data", "instruments", "instrument");
        var instruments = new List<string>();

        if (payload.ValueKind == JsonValueKind.Array)
        {
            instruments.AddRange(payload
                .EnumerateArray()
                .Where(item => item.ValueKind == JsonValueKind.Object)
                .Select(item => item.GetRawText()));

            return instruments;
        }

        if (payload.ValueKind == JsonValueKind.Object)
        {
            instruments.Add(payload.GetRawText());
        }

        return instruments;
    }

    private static bool MatchesSymbol(JsonElement element, string symbol)
    {
        var candidate = GetString(element, "symbol", "code", "ticker");
        return candidate is not null && candidate.Equals(symbol, StringComparison.OrdinalIgnoreCase);
    }

    private static IReadOnlyList<PriceLevelDto> GetPriceLevels(
        JsonElement element,
        IReadOnlyCollection<string> arrayPropertyNames,
        string prefix)
    {
        foreach (var propertyName in arrayPropertyNames)
        {
            if (TryGetProperty(element, propertyName, out var levelsElement) &&
                levelsElement.ValueKind == JsonValueKind.Array)
            {
                return levelsElement
                    .EnumerateArray()
                    .Select(level => new PriceLevelDto(
                        GetDecimal(level, "price", "bidPrice", "askPrice"),
                        GetLong(level, "quantity", "qtty", "volume")))
                    .Where(level => level.Price > 0m || level.Quantity > 0)
                    .Take(3)
                    .ToArray();
            }
        }

        var numberedLevels = new List<PriceLevelDto>();
        for (var level = 1; level <= 3; level++)
        {
            var price = GetDecimal(element, $"{prefix}Price{level}", $"{prefix}{level}Price");
            var quantity = GetLong(element, $"{prefix}Quantity{level}", $"{prefix}Qtty{level}", $"{prefix}Volume{level}");
            if (price > 0m || quantity > 0)
            {
                numberedLevels.Add(new PriceLevelDto(price, quantity));
            }
        }

        return numberedLevels;
    }

    private static JsonElement UnwrapPayload(JsonElement? element, params string[] payloadPropertyNames)
    {
        if (element is null)
        {
            return default;
        }

        return UnwrapPayload(element.Value, payloadPropertyNames);
    }

    private static JsonElement UnwrapPayload(JsonElement current, params string[] payloadPropertyNames)
    {
        if (current.ValueKind == JsonValueKind.Object)
        {
            foreach (var propertyName in payloadPropertyNames)
            {
                if (!propertyName.Equals("data", StringComparison.OrdinalIgnoreCase) &&
                    TryGetProperty(current, propertyName, out var payload))
                {
                    return payload;
                }
            }

            if (TryGetProperty(current, "data", out var dataPayload))
            {
                return dataPayload.ValueKind == JsonValueKind.Object
                    ? UnwrapPayload(dataPayload, payloadPropertyNames)
                    : dataPayload;
            }
        }

        return current;
    }

    private static decimal GetPriceScaleAnchor(
        IReadOnlyCollection<decimal> prices,
        IReadOnlyCollection<PriceLevelDto> bidLevels,
        IReadOnlyCollection<PriceLevelDto> askLevels)
    {
        return prices
            .Concat(bidLevels.Select(level => level.Price))
            .Concat(askLevels.Select(level => level.Price))
            .Where(price => price > 0m)
            .DefaultIfEmpty(0m)
            .Max();
    }

    private static decimal NormalizePriceScale(decimal value, decimal anchor)
    {
        if (value <= 0m || anchor < 1000m || value >= 1000m)
        {
            return value;
        }

        return value * 1000m;
    }

    private static decimal NormalizePriceDeltaScale(decimal value, decimal anchor)
    {
        if (value == 0m || anchor < 1000m || Math.Abs(value) >= 1000m)
        {
            return value;
        }

        return value * 1000m;
    }

    private static IReadOnlyList<PriceLevelDto> NormalizePriceLevels(
        IReadOnlyList<PriceLevelDto> levels,
        decimal anchor,
        int quantityScaleFactor)
    {
        return levels
            .Select(level => new PriceLevelDto(
                NormalizePriceScale(level.Price, anchor),
                ScaleQuantity(level.Quantity, quantityScaleFactor)))
            .ToArray();
    }

    private static long ScaleQuantity(long value, int quantityScaleFactor)
    {
        return value <= 0 || quantityScaleFactor <= 1
            ? value
            : checked(value * quantityScaleFactor);
    }

    private static JsonElement FirstObjectOrSelf(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.Array)
        {
            return element;
        }

        foreach (var item in element.EnumerateArray())
        {
            if (item.ValueKind == JsonValueKind.Object)
            {
                return item;
            }
        }

        return default;
    }

    private static JsonElement SelectForeignTradingPayload(JsonElement element, string symbol, string boardId)
    {
        if (element.ValueKind != JsonValueKind.Array)
        {
            return element;
        }

        JsonElement? selected = null;
        DateTimeOffset? selectedTime = null;
        var selectedScore = -1;

        foreach (var item in element.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            var score = GetForeignTradingMatchScore(item, symbol, boardId);
            var time = GetDateTimeOffset(item, "updatedAt", "time", "timestamp", "tradingTime", "transactTime");

            if (selected is null ||
                score > selectedScore ||
                (score == selectedScore && time is not null && (selectedTime is null || time > selectedTime)))
            {
                selected = item;
                selectedTime = time;
                selectedScore = score;
            }
        }

        return selected ?? default;
    }

    private static int GetForeignTradingMatchScore(JsonElement element, string symbol, string boardId)
    {
        var itemSymbol = NormalizeToken(GetString(element, "symbol") ?? string.Empty, string.Empty);
        var itemBoardId = NormalizeToken(GetString(element, "boardId") ?? string.Empty, string.Empty);

        if (itemSymbol.Equals(symbol, StringComparison.Ordinal) &&
            itemBoardId.Equals(boardId, StringComparison.Ordinal))
        {
            return 2;
        }

        if (itemSymbol.Equals(symbol, StringComparison.Ordinal))
        {
            return 1;
        }

        return 0;
    }

    private static string? GetString(JsonElement element, params string[] propertyNames)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        foreach (var propertyName in propertyNames)
        {
            if (TryGetProperty(element, propertyName, out var property))
            {
                return property.ValueKind switch
                {
                    JsonValueKind.String => property.GetString(),
                    JsonValueKind.Number => property.GetRawText(),
                    _ => null
                };
            }
        }

        return null;
    }

    private static decimal GetDecimal(JsonElement element, params string[] propertyNames)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            return 0m;
        }

        foreach (var propertyName in propertyNames)
        {
            if (TryGetProperty(element, propertyName, out var property))
            {
                if (property.ValueKind == JsonValueKind.Number && property.TryGetDecimal(out var number))
                {
                    return number;
                }

                if (property.ValueKind == JsonValueKind.String &&
                    decimal.TryParse(property.GetString(), out var stringNumber))
                {
                    return stringNumber;
                }
            }
        }

        return 0m;
    }

    private static long GetLong(JsonElement element, params string[] propertyNames)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            return 0;
        }

        foreach (var propertyName in propertyNames)
        {
            if (TryGetProperty(element, propertyName, out var property))
            {
                if (property.ValueKind == JsonValueKind.Number && property.TryGetInt64(out var number))
                {
                    return number;
                }

                if (property.ValueKind == JsonValueKind.String &&
                    long.TryParse(property.GetString(), out var stringNumber))
                {
                    return stringNumber;
                }
            }
        }

        return 0;
    }

    private static DateTimeOffset? GetDateTimeOffset(JsonElement element, params string[] propertyNames)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        foreach (var propertyName in propertyNames)
        {
            if (!TryGetProperty(element, propertyName, out var property))
            {
                continue;
            }

            if (property.ValueKind == JsonValueKind.String &&
                DateTimeOffset.TryParse(property.GetString(), out var parsedDateTime))
            {
                return parsedDateTime;
            }

            if (property.ValueKind == JsonValueKind.Number && property.TryGetInt64(out var unixTime))
            {
                return unixTime > 10_000_000_000
                    ? DateTimeOffset.FromUnixTimeMilliseconds(unixTime)
                    : DateTimeOffset.FromUnixTimeSeconds(unixTime);
            }
        }

        return null;
    }

    private static bool TryGetProperty(JsonElement element, string propertyName, out JsonElement property)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            foreach (var item in element.EnumerateObject())
            {
                if (item.Name.Equals(propertyName, StringComparison.OrdinalIgnoreCase))
                {
                    property = item.Value;
                    return true;
                }
            }
        }

        property = default;
        return false;
    }

    private static string NormalizeSymbol(string symbol)
    {
        return NormalizeToken(symbol, string.Empty);
    }

    private static string NormalizeToken(string value, string fallback)
    {
        return string.IsNullOrWhiteSpace(value)
            ? fallback
            : value.Trim().ToUpperInvariant();
    }
}
