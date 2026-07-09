using InvestView.Application.Dtos.MarketData;

namespace InvestView.Infrastructure.MarketData;

internal static class MarketStateMapper
{
    public static MarketQuoteDto NormalizeQuote(MarketQuoteDto quote)
    {
        return quote with
        {
            Symbol = Normalize(quote.Symbol),
            BoardId = NormalizeBoardId(quote.BoardId),
            MarketId = Normalize(quote.MarketId)
        };
    }

    public static MarketQuoteUpdateDto NormalizeQuoteUpdate(MarketQuoteUpdateDto update)
    {
        return update with
        {
            Symbol = Normalize(update.Symbol),
            BoardId = NormalizeBoardId(update.BoardId)
        };
    }

    public static MarketTradeUpdateDto NormalizeTradeUpdate(MarketTradeUpdateDto update)
    {
        return update with
        {
            Symbol = Normalize(update.Symbol),
            BoardId = NormalizeBoardId(update.BoardId),
            Side = Normalize(update.Side)
        };
    }

    public static MarketIndexDto NormalizeIndex(MarketIndexDto index)
    {
        return index with
        {
            IndexName = Normalize(index.IndexName),
            MarketId = Normalize(index.MarketId)
        };
    }

    public static MarketIndexUpdateDto NormalizeIndexUpdate(MarketIndexUpdateDto update)
    {
        return update with
        {
            IndexName = Normalize(update.IndexName),
            MarketId = Normalize(update.MarketId),
            TradingSessionId = Normalize(update.TradingSessionId)
        };
    }

    public static IReadOnlyCollection<string> NormalizeSymbols(IReadOnlyCollection<string> symbols)
    {
        return symbols
            .SelectMany(symbol => symbol.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            .Select(Normalize)
            .Where(symbol => !string.IsNullOrWhiteSpace(symbol))
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();
    }

    public static string QuoteKey(string boardId, string symbol)
    {
        return $"{NormalizeBoardId(boardId)}:{Normalize(symbol)}";
    }

    public static string NormalizeBoardId(string boardId)
    {
        return string.IsNullOrWhiteSpace(boardId) ? MockMarketDataProvider.DefaultBoardId : Normalize(boardId);
    }

    public static string Normalize(string value)
    {
        return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim().ToUpperInvariant();
    }

    public static MarketQuoteDto MergeQuote(MarketQuoteDto current, MarketQuoteUpdateDto update)
    {
        var lastPrice = update.LastPrice ?? current.LastPrice;
        var referencePrice = update.ReferencePrice ?? current.ReferencePrice;
        var change = update.Change ?? (referencePrice > 0m ? lastPrice - referencePrice : current.Change);
        var changePercent = update.ChangePercent ?? (referencePrice > 0m
            ? Math.Round(change / referencePrice * 100m, 2, MidpointRounding.AwayFromZero)
            : current.ChangePercent);

        return current with
        {
            ReferencePrice = referencePrice,
            CeilingPrice = update.CeilingPrice ?? current.CeilingPrice,
            FloorPrice = update.FloorPrice ?? current.FloorPrice,
            LastPrice = lastPrice,
            Change = change,
            ChangePercent = changePercent,
            LastQuantity = update.LastQuantity ?? current.LastQuantity,
            TotalVolume = update.TotalVolume ?? current.TotalVolume,
            TotalValue = update.TotalValue ?? current.TotalValue,
            ForeignBuyVolume = update.ForeignBuyVolume ?? current.ForeignBuyVolume,
            ForeignSellVolume = update.ForeignSellVolume ?? current.ForeignSellVolume,
            ForeignRoom = update.ForeignRoom ?? current.ForeignRoom,
            OpenPrice = update.OpenPrice ?? current.OpenPrice,
            HighPrice = update.HighPrice ?? current.HighPrice,
            LowPrice = update.LowPrice ?? current.LowPrice,
            BidLevels = update.BidLevels ?? current.BidLevels,
            AskLevels = update.AskLevels ?? current.AskLevels,
            TradingStatus = update.TradingStatus ?? current.TradingStatus,
            UpdatedAt = update.UpdatedAt
        };
    }

    public static MarketQuoteDto CreateQuoteFromUpdate(MarketQuoteUpdateDto update)
    {
        var lastPrice = update.LastPrice ?? 0m;
        var referencePrice = update.ReferencePrice ?? 0m;
        var change = update.Change ?? (referencePrice > 0m ? lastPrice - referencePrice : 0m);

        return new MarketQuoteDto(
            Symbol: update.Symbol,
            BoardId: update.BoardId,
            MarketId: string.Empty,
            DisplayName: update.Symbol,
            ReferencePrice: referencePrice,
            CeilingPrice: update.CeilingPrice ?? 0m,
            FloorPrice: update.FloorPrice ?? 0m,
            LastPrice: lastPrice,
            Change: change,
            ChangePercent: update.ChangePercent ?? (referencePrice > 0m
                ? Math.Round(change / referencePrice * 100m, 2, MidpointRounding.AwayFromZero)
                : 0m),
            LastQuantity: update.LastQuantity ?? 0,
            TotalVolume: update.TotalVolume ?? 0,
            TotalValue: update.TotalValue ?? 0m,
            ForeignBuyVolume: update.ForeignBuyVolume ?? 0,
            ForeignSellVolume: update.ForeignSellVolume ?? 0,
            ForeignRoom: update.ForeignRoom ?? 0,
            OpenPrice: update.OpenPrice ?? 0m,
            HighPrice: update.HighPrice ?? lastPrice,
            LowPrice: update.LowPrice ?? lastPrice,
            BidLevels: update.BidLevels ?? [],
            AskLevels: update.AskLevels ?? [],
            TradingStatus: update.TradingStatus ?? string.Empty,
            UpdatedAt: update.UpdatedAt);
    }

    public static MarketQuoteUpdateDto CreateQuoteUpdateFromTrade(MarketTradeUpdateDto update)
    {
        return new MarketQuoteUpdateDto(
            update.Symbol,
            update.BoardId,
            LastPrice: update.Price,
            Change: update.Change,
            ChangePercent: update.ChangePercent,
            LastQuantity: update.Quantity,
            TotalVolume: update.TotalVolume,
            TotalValue: update.TotalValue,
            ForeignBuyVolume: null,
            ForeignSellVolume: null,
            ForeignRoom: null,
            BidLevels: null,
            AskLevels: null,
            TradingStatus: null,
            UpdatedAt: update.Time);
    }

    public static MarketQuoteUpdateDto CreateQuoteUpdateFromQuote(MarketQuoteDto quote)
    {
        return new MarketQuoteUpdateDto(
            quote.Symbol,
            quote.BoardId,
            quote.LastPrice,
            quote.Change,
            quote.ChangePercent,
            quote.LastQuantity,
            quote.TotalVolume,
            quote.TotalValue,
            quote.ForeignBuyVolume,
            quote.ForeignSellVolume,
            quote.ForeignRoom,
            quote.BidLevels,
            quote.AskLevels,
            quote.TradingStatus,
            quote.UpdatedAt,
            quote.ReferencePrice,
            quote.CeilingPrice,
            quote.FloorPrice,
            quote.OpenPrice,
            quote.HighPrice,
            quote.LowPrice);
    }

    public static MarketTradeDto CreateTradeFromUpdate(MarketTradeUpdateDto update)
    {
        return new MarketTradeDto(
            update.Symbol,
            update.BoardId,
            update.Time,
            update.Price ?? 0m,
            update.Change ?? 0m,
            update.ChangePercent ?? 0m,
            update.Quantity ?? 0,
            update.TotalVolume ?? 0,
            update.TotalValue ?? 0m,
            update.Side);
    }

    public static MarketIndexDto MergeIndex(MarketIndexDto current, MarketIndexUpdateDto update)
    {
        return current with
        {
            Value = update.Value ?? current.Value,
            Change = update.Change ?? current.Change,
            ChangePercent = update.ChangePercent ?? current.ChangePercent,
            ReferenceValue = update.ReferenceValue ?? current.ReferenceValue,
            HighValue = update.HighValue ?? current.HighValue,
            LowValue = update.LowValue ?? current.LowValue,
            TotalVolume = update.TotalVolume ?? current.TotalVolume,
            TotalValue = update.TotalValue ?? current.TotalValue,
            UpCount = update.UpCount ?? current.UpCount,
            DownCount = update.DownCount ?? current.DownCount,
            NoChangeCount = update.NoChangeCount ?? current.NoChangeCount,
            CeilingCount = update.CeilingCount ?? current.CeilingCount,
            FloorCount = update.FloorCount ?? current.FloorCount,
            MarketId = string.IsNullOrWhiteSpace(update.MarketId) ? current.MarketId : update.MarketId,
            TradingSessionId = string.IsNullOrWhiteSpace(update.TradingSessionId) ? current.TradingSessionId : update.TradingSessionId,
            UpdatedAt = update.UpdatedAt
        };
    }

    public static MarketIndexDto CreateIndexFromUpdate(MarketIndexUpdateDto update)
    {
        return new MarketIndexDto(
            update.IndexName,
            update.Value,
            update.Change,
            update.ChangePercent,
            update.ReferenceValue,
            update.HighValue,
            update.LowValue,
            update.TotalVolume,
            update.TotalValue,
            update.UpCount,
            update.DownCount,
            update.NoChangeCount,
            update.CeilingCount,
            update.FloorCount,
            update.MarketId,
            update.TradingSessionId,
            update.UpdatedAt);
    }

    public static MarketIndexUpdateDto CreateIndexUpdateFromIndex(MarketIndexDto index)
    {
        return new MarketIndexUpdateDto(
            index.IndexName,
            index.Value,
            index.Change,
            index.ChangePercent,
            index.ReferenceValue,
            index.HighValue,
            index.LowValue,
            index.TotalVolume,
            index.TotalValue,
            index.UpCount,
            index.DownCount,
            index.NoChangeCount,
            index.CeilingCount,
            index.FloorCount,
            index.MarketId,
            index.TradingSessionId,
            index.UpdatedAt);
    }
}
