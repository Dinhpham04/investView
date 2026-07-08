using InvestView.Application.Dtos.MarketData;

namespace InvestView.Infrastructure.Dnse;

public sealed class DnseQuoteUpdateAggregator
{
    private readonly object _syncRoot = new();
    private readonly Dictionary<string, QuoteState> _states = new(StringComparer.Ordinal);

    public MarketQuoteUpdateDto Apply(MarketQuoteUpdateDto update)
    {
        lock (_syncRoot)
        {
            var key = BuildKey(update.Symbol, update.BoardId);
            _states.TryGetValue(key, out var currentState);

            var referencePrice = update.ReferencePrice ?? currentState?.ReferencePrice;
            var lastPrice = update.LastPrice ?? currentState?.LastPrice;
            var enrichedUpdate = EnrichChangeFields(update, referencePrice);

            _states[key] = new QuoteState(
                ReferencePrice: referencePrice,
                LastPrice: lastPrice);

            return enrichedUpdate;
        }
    }

    private static MarketQuoteUpdateDto EnrichChangeFields(MarketQuoteUpdateDto update, decimal? referencePrice)
    {
        if (update.LastPrice is null || referencePrice is null or <= 0m)
        {
            return update;
        }

        var change = update.LastPrice.Value - referencePrice.Value;
        var changePercent = Math.Round(change / referencePrice.Value * 100m, 2, MidpointRounding.AwayFromZero);

        return update with
        {
            Change = update.Change ?? change,
            ChangePercent = update.ChangePercent ?? changePercent
        };
    }

    private static string BuildKey(string symbol, string boardId)
    {
        return $"{Normalize(boardId)}:{Normalize(symbol)}";
    }

    private static string Normalize(string value)
    {
        return value.Trim().ToUpperInvariant();
    }

    private sealed record QuoteState(decimal? ReferencePrice, decimal? LastPrice);
}
