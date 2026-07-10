using InvestView.Application.Abstractions.MarketData;

namespace InvestView.Infrastructure.Dnse;

public static class DnseWebSocketSubscriptionBuilder
{
    public static DnseWebSocketSubscribePayload BuildSubscribePayload(
        IReadOnlyCollection<string> symbols,
        string boardId,
        string encoding,
        IReadOnlyCollection<MarketDataChannel> channels,
        string productGroupId = "STO")
    {
        var normalizedSymbols = NormalizeSymbols(symbols);
        var subscriptions = channels
            .Select(channel => new DnseWebSocketChannelSubscription(
                BuildChannelName(channel, boardId, encoding, productGroupId),
                normalizedSymbols))
            .ToArray();

        return new DnseWebSocketSubscribePayload("subscribe", subscriptions);
    }

    public static DnseWebSocketSubscribePayload BuildMarketIndexSubscribePayload(
        IReadOnlyCollection<string> indexNames,
        string encoding)
    {
        var normalizedIndexNames = NormalizeSymbols(indexNames);
        var normalizedEncoding = NormalizeToken(encoding, "json").ToLowerInvariant();
        var subscriptions = normalizedIndexNames
            .Select(indexName => new DnseWebSocketChannelSubscription(
                $"market_index.{indexName}.{normalizedEncoding}",
                []))
            .ToArray();

        return new DnseWebSocketSubscribePayload("subscribe", subscriptions);
    }

    public static DnseWebSocketSubscribePayload BuildEstimatedMarketIndexSubscribePayload(
        IReadOnlyCollection<string> indexNames,
        string encoding)
    {
        var normalizedIndexNames = NormalizeSymbols(indexNames);
        var normalizedEncoding = NormalizeToken(encoding, "json").ToLowerInvariant();
        var subscriptions = normalizedIndexNames
            .Select(indexName => new DnseWebSocketChannelSubscription(
                $"estimated_market_index.{indexName}.{normalizedEncoding}",
                []))
            .ToArray();

        return new DnseWebSocketSubscribePayload("subscribe", subscriptions);
    }

    public static DnseWebSocketSubscribePayload BuildOhlcSubscribePayload(
        IReadOnlyCollection<string> symbols,
        IReadOnlyCollection<string> resolutions,
        string encoding,
        bool closed)
    {
        var normalizedSymbols = NormalizeSymbols(symbols);
        var normalizedEncoding = NormalizeToken(encoding, "json").ToLowerInvariant();
        var prefix = closed ? "ohlc_closed" : "ohlc";
        var subscriptions = NormalizeResolutions(resolutions)
            .Select(resolution => new DnseWebSocketChannelSubscription(
                $"{prefix}.{resolution}.{normalizedEncoding}",
                normalizedSymbols))
            .ToArray();

        return new DnseWebSocketSubscribePayload("subscribe", subscriptions);
    }

    public static DnseWebSocketSubscribePayload BuildSessionSubscribePayload(
        IReadOnlyCollection<string> boardIds,
        string productGroupId,
        string encoding)
    {
        var normalizedBoards = NormalizeSymbols(boardIds);
        var normalizedProductGroupId = NormalizeToken(productGroupId, "STO");
        var normalizedEncoding = NormalizeToken(encoding, "json").ToLowerInvariant();
        var subscriptions = normalizedBoards
            .Select(boardId => new DnseWebSocketChannelSubscription(
                $"session.{normalizedProductGroupId}.{boardId}.{normalizedEncoding}",
                []))
            .ToArray();

        return new DnseWebSocketSubscribePayload("subscribe", subscriptions);
    }

    public static string BuildChannelName(
        MarketDataChannel channel,
        string boardId,
        string encoding,
        string productGroupId = "STO")
    {
        var normalizedBoardId = NormalizeToken(boardId, "G1");
        var normalizedEncoding = NormalizeToken(encoding, "json").ToLowerInvariant();

        return channel switch
        {
            MarketDataChannel.SecurityDefinition => $"security_definition.{normalizedBoardId}.{normalizedEncoding}",
            MarketDataChannel.Trade => $"tick.{normalizedBoardId}.{normalizedEncoding}",
            MarketDataChannel.TradeExtra => $"tick_extra.{normalizedBoardId}.{normalizedEncoding}",
            MarketDataChannel.TopPrice => $"top_price.{normalizedBoardId}.{normalizedEncoding}",
            MarketDataChannel.Foreign => $"foreign.{normalizedBoardId}.{normalizedEncoding}",
            MarketDataChannel.ExpectedPrice => $"expected_price.{normalizedBoardId}.{normalizedEncoding}",
            MarketDataChannel.MarketIndex => throw new ArgumentException("Use BuildMarketIndexSubscribePayload for market index channels.", nameof(channel)),
            MarketDataChannel.EstimatedMarketIndex => throw new ArgumentException("Use BuildEstimatedMarketIndexSubscribePayload for estimated market index channels.", nameof(channel)),
            MarketDataChannel.Session => $"session.{NormalizeToken(productGroupId, "STO")}.{normalizedBoardId}.{normalizedEncoding}",
            MarketDataChannel.Ohlc => $"ohlc.1.{normalizedEncoding}",
            MarketDataChannel.OhlcClosed => $"ohlc_closed.1.{normalizedEncoding}",
            _ => throw new ArgumentOutOfRangeException(nameof(channel), channel, "Unsupported DNSE websocket channel.")
        };
    }

    private static IReadOnlyList<string> NormalizeSymbols(IReadOnlyCollection<string> symbols)
    {
        return symbols
            .SelectMany(symbol => symbol.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            .Select(symbol => NormalizeToken(symbol, string.Empty))
            .Where(symbol => !string.IsNullOrWhiteSpace(symbol))
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();
    }

    private static IReadOnlyList<string> NormalizeResolutions(IReadOnlyCollection<string> resolutions)
    {
        return resolutions
            .Select(resolution => NormalizeToken(resolution, "1"))
            .Where(resolution => !string.IsNullOrWhiteSpace(resolution))
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();
    }

    private static string NormalizeToken(string value, string fallback)
    {
        return string.IsNullOrWhiteSpace(value)
            ? fallback
            : value.Trim().ToUpperInvariant();
    }
}

public sealed record DnseWebSocketSubscribePayload(
    string Action,
    IReadOnlyList<DnseWebSocketChannelSubscription> Channels);

public sealed record DnseWebSocketChannelSubscription(
    string Name,
    IReadOnlyList<string> Symbols);
