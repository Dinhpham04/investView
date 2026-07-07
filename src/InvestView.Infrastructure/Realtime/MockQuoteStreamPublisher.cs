using InvestView.Application.Abstractions.MarketData;
using InvestView.Application.Abstractions.Realtime;
using InvestView.Application.Dtos.MarketData;
using InvestView.Application.Dtos.Realtime;
using Microsoft.Extensions.Options;

namespace InvestView.Infrastructure.Realtime;

public sealed class MockQuoteStreamPublisher
{
    private readonly IMarketDataProvider _marketDataProvider;
    private readonly IMarketQuoteBroadcaster _broadcaster;
    private readonly MarketQuoteStreamOptions _options;
    private readonly TimeProvider _timeProvider;
    private long _sequence;

    public MockQuoteStreamPublisher(
        IMarketDataProvider marketDataProvider,
        IMarketQuoteBroadcaster broadcaster,
        IOptions<MarketQuoteStreamOptions> options,
        TimeProvider timeProvider)
    {
        _marketDataProvider = marketDataProvider;
        _broadcaster = broadcaster;
        _options = options.Value;
        _timeProvider = timeProvider;
    }

    public async Task<int> PublishOnceAsync(CancellationToken cancellationToken)
    {
        var sequence = Interlocked.Increment(ref _sequence);
        var query = new MarketBoardQuery(
            NormalizeSymbols(_options.Symbols),
            NormalizeToken(_options.BoardId, "G1"));
        var quotes = await _marketDataProvider.GetMarketBoardAsync(query, cancellationToken);
        var now = _timeProvider.GetUtcNow();
        var published = 0;

        foreach (var quote in quotes)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var update = CreateUpdate(quote, sequence, now);
            await _broadcaster.BroadcastQuoteUpdateAsync(update, cancellationToken);
            published++;
        }

        await _broadcaster.BroadcastStreamStatusAsync(
            new QuoteStreamStatusDto(
                Provider: "Mock",
                IsEnabled: true,
                UpdatedAt: now,
                Message: $"Published {published} quote update(s)."),
            cancellationToken);

        return published;
    }

    private static MarketQuoteUpdateDto CreateUpdate(
        MarketQuoteDto quote,
        long sequence,
        DateTimeOffset updatedAt)
    {
        var priceStep = quote.LastPrice >= 1_000m ? 50m : 0.05m;
        var direction = (sequence + StableSymbolOffset(quote.Symbol)) % 3 - 1;
        var lastPrice = Math.Max(0m, quote.LastPrice + direction * priceStep);
        var change = quote.ReferencePrice > 0m ? lastPrice - quote.ReferencePrice : quote.Change;
        var changePercent = quote.ReferencePrice > 0m
            ? Math.Round(change / quote.ReferencePrice * 100m, 2, MidpointRounding.AwayFromZero)
            : quote.ChangePercent;
        var lastQuantity = Math.Max(0, quote.LastQuantity + direction * 10);
        var totalVolume = Math.Max(0, quote.TotalVolume + Math.Abs(direction) * 100);

        return new MarketQuoteUpdateDto(
            Symbol: quote.Symbol,
            BoardId: quote.BoardId,
            LastPrice: lastPrice,
            Change: change,
            ChangePercent: changePercent,
            LastQuantity: lastQuantity,
            TotalVolume: totalVolume,
            TotalValue: quote.TotalValue,
            ForeignBuyVolume: quote.ForeignBuyVolume,
            ForeignSellVolume: quote.ForeignSellVolume,
            ForeignRoom: quote.ForeignRoom,
            BidLevels: quote.BidLevels,
            AskLevels: quote.AskLevels,
            TradingStatus: quote.TradingStatus,
            UpdatedAt: updatedAt);
    }

    private static int StableSymbolOffset(string symbol)
    {
        return symbol.Aggregate(0, (current, character) => current + character);
    }

    private static IReadOnlyCollection<string> NormalizeSymbols(IReadOnlyCollection<string> symbols)
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
