using System.Globalization;
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
        JsonElement? latestTrade,
        JsonElement? latestQuote,
        JsonElement? foreignTrading,
        DateTimeOffset fallbackUpdatedAt,
        int quantityScaleFactor = 1)
    {
        var normalizedSymbol = NormalizeSymbol(symbol);
        var instrumentPayload = FirstObjectOrSelf(UnwrapPayload(instrument, "data", "instruments", "instrument"));
        var secdefPayload = FirstObjectOrSelf(UnwrapPayload(securityDefinition, "data", "secdef", "secdefs", "securityDefinition", "securityDefinitions"));
        var quote = MapMarketQuote(
            normalizedSymbol,
            boardId,
            instrument,
            securityDefinition,
            latestTrade,
            latestQuote,
            foreignTrading,
            fallbackUpdatedAt,
            quantityScaleFactor);

        return new SymbolDetailDto(
            Symbol: quote.Symbol,
            BoardId: quote.BoardId,
            MarketId: quote.MarketId,
            DisplayName: quote.DisplayName,
            Name: GetString(instrumentPayload, "fullName", "organName", "companyName", "name", "symbolName") ?? quote.DisplayName,
            SecurityType: GetString(instrumentPayload, "securityType", "type", "securityGroupId") ?? "Stock",
            Isin: GetString(secdefPayload, "isin") ?? GetString(instrumentPayload, "isin") ?? string.Empty,
            ProductGroupId: GetString(secdefPayload, "productGrpId", "productGroupId") ?? GetString(instrumentPayload, "productGrpId", "productGroupId") ?? string.Empty,
            SecurityGroupId: GetString(secdefPayload, "securityGroupId") ?? GetString(instrumentPayload, "securityGroupId") ?? string.Empty,
            ReferencePrice: quote.ReferencePrice,
            CeilingPrice: quote.CeilingPrice,
            FloorPrice: quote.FloorPrice,
            LastPrice: quote.LastPrice,
            Change: quote.Change,
            ChangePercent: quote.ChangePercent,
            LastQuantity: quote.LastQuantity,
            TotalVolume: quote.TotalVolume,
            TotalValue: quote.TotalValue,
            ForeignBuyVolume: quote.ForeignBuyVolume,
            ForeignSellVolume: quote.ForeignSellVolume,
            ForeignRoom: quote.ForeignRoom,
            OpenPrice: quote.OpenPrice,
            HighPrice: quote.HighPrice,
            LowPrice: quote.LowPrice,
            BidLevels: quote.BidLevels,
            AskLevels: quote.AskLevels,
            TradingStatus: quote.TradingStatus,
            SymbolAdminStatus: GetString(secdefPayload, "symbolAdminStatusCode", "symbolAdminStatus", "adminStatus") ?? string.Empty,
            TradingMethodStatus: GetString(secdefPayload, "symbolTradingMethodStatusCode", "symbolTradingMethodStatus", "tradingMethodStatus") ?? string.Empty,
            TradingSanctionStatus: GetString(secdefPayload, "symbolTradingSanctionStatusCode", "symbolTradingSanctionStatus", "tradingSanctionStatus") ?? string.Empty,
            ListingDate: GetDateTimeOffset(secdefPayload, "listingDate") ?? GetDateTimeOffset(instrumentPayload, "listingDate", "listedDate"),
            FinalTradeDate: GetDateTimeOffset(secdefPayload, "finalTradeDate"),
            OpenInterestQuantity: GetLong(secdefPayload, "openInterestQuantity", "openInterest"),
            UpdatedAt: quote.UpdatedAt);
    }

    public static IReadOnlyList<OhlcBarDto> MapOhlcBars(
        string symbol,
        string resolution,
        JsonElement root,
        int quantityScaleFactor = 1)
    {
        var normalizedSymbol = NormalizeSymbol(symbol);
        var normalizedResolution = NormalizeToken(resolution, string.Empty);
        var payload = UnwrapPayload(root, "data", "ohlc", "ohlcs", "bars", "candles");
        var normalizedQuantityScaleFactor = Math.Max(quantityScaleFactor, 1);

        if (TryMapColumnarOhlcBars(
            normalizedSymbol,
            normalizedResolution,
            payload,
            normalizedQuantityScaleFactor,
            out var columnarBars))
        {
            return columnarBars;
        }

        return EnumerateObjects(payload)
            .Select(item =>
            {
                var open = NormalizeStockPriceScale(GetDecimal(item, "open", "o"));
                var high = NormalizeStockPriceScale(GetDecimal(item, "high", "h"));
                var low = NormalizeStockPriceScale(GetDecimal(item, "low", "l"));
                var close = NormalizeStockPriceScale(GetDecimal(item, "close", "c"));
                var time = GetDateTimeOffset(item, "time", "timestamp", "t", "lastUpdated");
                if (time is null)
                {
                    return null;
                }

                return new OhlcBarDto(
                    Symbol: NormalizeToken(GetString(item, "symbol") ?? normalizedSymbol, normalizedSymbol),
                    Resolution: NormalizeToken(GetString(item, "resolution") ?? normalizedResolution, normalizedResolution),
                    Time: time.Value,
                    Open: open,
                    High: high,
                    Low: low,
                    Close: close,
                    Volume: ScaleQuantity(GetLong(item, "volume", "v"), normalizedQuantityScaleFactor));
            })
            .Where(bar => bar is not null)
            .Select(bar => bar!)
            .OrderBy(bar => bar.Time)
            .ToArray();
    }

    private static bool TryMapColumnarOhlcBars(
        string normalizedSymbol,
        string normalizedResolution,
        JsonElement payload,
        int quantityScaleFactor,
        out IReadOnlyList<OhlcBarDto> bars)
    {
        bars = [];
        if (payload.ValueKind != JsonValueKind.Object)
        {
            return false;
        }

        if (!TryGetArray(payload, ["time", "timestamp", "t"], out var times) ||
            !TryGetArray(payload, ["open", "o"], out var opens) ||
            !TryGetArray(payload, ["high", "h"], out var highs) ||
            !TryGetArray(payload, ["low", "l"], out var lows) ||
            !TryGetArray(payload, ["close", "c"], out var closes))
        {
            return false;
        }

        TryGetArray(payload, ["volume", "v"], out var volumes);

        var symbol = NormalizeToken(GetString(payload, "symbol") ?? normalizedSymbol, normalizedSymbol);
        var resolution = NormalizeToken(GetString(payload, "resolution") ?? normalizedResolution, normalizedResolution);
        var count = new[] { times.Length, opens.Length, highs.Length, lows.Length, closes.Length }.Min();
        var mappedBars = new List<OhlcBarDto>(count);

        for (var index = 0; index < count; index++)
        {
            var time = GetDateTimeOffsetValue(times[index]);
            if (time is null)
            {
                continue;
            }

            var volume = index < volumes.Length
                ? ScaleQuantity(GetLongValue(volumes[index]), quantityScaleFactor)
                : 0;

            mappedBars.Add(new OhlcBarDto(
                Symbol: symbol,
                Resolution: resolution,
                Time: time.Value,
                Open: NormalizeStockPriceScale(GetDecimalValue(opens[index])),
                High: NormalizeStockPriceScale(GetDecimalValue(highs[index])),
                Low: NormalizeStockPriceScale(GetDecimalValue(lows[index])),
                Close: NormalizeStockPriceScale(GetDecimalValue(closes[index])),
                Volume: volume));
        }

        bars = mappedBars
            .OrderBy(bar => bar.Time)
            .ToArray();
        return true;
    }

    public static IReadOnlyList<MarketTradeDto> MapLatestTrades(
        string symbol,
        string boardId,
        JsonElement root,
        DateTimeOffset fallbackUpdatedAt,
        int quantityScaleFactor = 1)
    {
        var normalizedSymbol = NormalizeSymbol(symbol);
        var normalizedBoardId = NormalizeToken(boardId, "G1");
        var payload = UnwrapPayload(root, "data", "trade", "trades", "ticks");
        var normalizedQuantityScaleFactor = Math.Max(quantityScaleFactor, 1);

        return EnumerateObjects(payload)
            .Select(item =>
            {
                var price = NormalizeStockPriceScale(GetDecimal(item, "lastPrice", "matchPrice", "price", "closePrice"));
                var change = NormalizeStockPriceDeltaScale(GetDecimal(item, "change", "changedValue", "priceChange"));
                var time = GetDateTimeOffset(item, "updatedAt", "time", "timestamp", "tradingTime", "transactTime")
                    ?? fallbackUpdatedAt;

                return new MarketTradeDto(
                    Symbol: NormalizeToken(GetString(item, "symbol") ?? normalizedSymbol, normalizedSymbol),
                    BoardId: NormalizeToken(GetString(item, "boardId") ?? normalizedBoardId, normalizedBoardId),
                    Time: time,
                    Price: price,
                    Change: change,
                    ChangePercent: GetDecimal(item, "changePercent", "changedPercent", "priceChangePercent"),
                    Quantity: ScaleQuantity(
                        GetLong(item, "lastQuantity", "matchQtty", "matchQuantity", "quantity", "qtty"),
                        normalizedQuantityScaleFactor),
                    TotalVolume: ScaleQuantity(
                        GetLong(item, "totalVolumeTraded", "totalVolume", "accumulatedVolume", "volume"),
                        normalizedQuantityScaleFactor),
                    TotalValue: GetDecimal(item, "grossTradeAmount", "totalValue", "accumulatedValue", "value"),
                    Side: NormalizeTradeSide(GetString(item, "side", "matchSide", "tradeSide", "buySell")));
            })
            .Where(trade =>
                trade.Symbol.Equals(normalizedSymbol, StringComparison.Ordinal) &&
                trade.BoardId.Equals(normalizedBoardId, StringComparison.Ordinal))
            .OrderByDescending(trade => trade.Time)
            .ToArray();
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

    private static IEnumerable<JsonElement> EnumerateObjects(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in element.EnumerateArray())
            {
                if (item.ValueKind == JsonValueKind.Object)
                {
                    yield return item;
                }
            }

            yield break;
        }

        if (element.ValueKind == JsonValueKind.Object)
        {
            yield return element;
        }
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
        if (value <= 0m || value >= 1000m)
        {
            return value;
        }

        return anchor > 0m ? value * 1000m : value;
    }

    private static decimal NormalizePriceDeltaScale(decimal value, decimal anchor)
    {
        if (value == 0m || Math.Abs(value) >= 1000m)
        {
            return value;
        }

        return anchor > 0m && Math.Abs(value) < 10m ? value * 1000m : value;
    }

    private static decimal NormalizeStockPriceScale(decimal value)
    {
        return value > 0m && value < 1000m
            ? value * 1000m
            : value;
    }

    private static decimal NormalizeStockPriceDeltaScale(decimal value)
    {
        return value != 0m && Math.Abs(value) < 10m
            ? value * 1000m
            : value;
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
                return GetDecimalValue(property);
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
                return GetLongValue(property);
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

            return GetDateTimeOffsetValue(property);
        }

        return null;
    }

    private static decimal GetDecimalValue(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Number && element.TryGetDecimal(out var number))
        {
            return number;
        }

        if (element.ValueKind == JsonValueKind.String)
        {
            var rawValue = element.GetString();
            if (decimal.TryParse(rawValue, NumberStyles.Number, CultureInfo.InvariantCulture, out var invariantNumber) ||
                decimal.TryParse(rawValue, out invariantNumber))
            {
                return invariantNumber;
            }
        }

        return 0m;
    }

    private static long GetLongValue(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Number && element.TryGetInt64(out var number))
        {
            return number;
        }

        if (element.ValueKind == JsonValueKind.String)
        {
            var rawValue = element.GetString();
            if (long.TryParse(rawValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out var invariantNumber) ||
                long.TryParse(rawValue, out invariantNumber))
            {
                return invariantNumber;
            }
        }

        return 0;
    }

    private static DateTimeOffset? GetDateTimeOffsetValue(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.String &&
            DateOnly.TryParse(element.GetString(), out var parsedDate))
        {
            return new DateTimeOffset(parsedDate.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
        }

        if (element.ValueKind == JsonValueKind.String &&
            DateTimeOffset.TryParse(element.GetString(), out var parsedDateTime))
        {
            return parsedDateTime;
        }

        if (element.ValueKind == JsonValueKind.Number && element.TryGetInt64(out var unixTime))
        {
            return unixTime > 10_000_000_000
                ? DateTimeOffset.FromUnixTimeMilliseconds(unixTime)
                : DateTimeOffset.FromUnixTimeSeconds(unixTime);
        }

        return null;
    }

    private static bool TryGetArray(JsonElement element, string[] propertyNames, out JsonElement[] items)
    {
        foreach (var propertyName in propertyNames)
        {
            if (TryGetProperty(element, propertyName, out var property) &&
                property.ValueKind == JsonValueKind.Array)
            {
                items = property.EnumerateArray().ToArray();
                return true;
            }
        }

        items = [];
        return false;
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

    private static string NormalizeTradeSide(string? side)
    {
        return string.IsNullOrWhiteSpace(side)
            ? string.Empty
            : side.Trim().ToUpperInvariant();
    }
}
