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
            MarketDataChannel.TopPrice => $"top_price.{normalizedBoardId}.{normalizedEncoding}",
            MarketDataChannel.Foreign => $"foreign.{normalizedBoardId}.{normalizedEncoding}",
            MarketDataChannel.Session => $"session.{NormalizeToken(productGroupId, "STO")}.{normalizedBoardId}.{normalizedEncoding}",
            MarketDataChannel.Ohlc => $"ohlc.1.{normalizedEncoding}",
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
