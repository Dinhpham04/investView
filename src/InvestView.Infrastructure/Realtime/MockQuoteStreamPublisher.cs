using InvestView.Application.Abstractions.MarketData;
using InvestView.Application.Abstractions.Realtime;
using InvestView.Application.Dtos.MarketData;
using InvestView.Application.Dtos.Realtime;
using InvestView.Infrastructure.MarketData;
using Microsoft.Extensions.Options;

namespace InvestView.Infrastructure.Realtime;

public sealed class MockQuoteStreamPublisher
{
    private readonly IMarketDataProvider _configuredMarketDataProvider;
    private readonly MockMarketDataProvider _mockMarketDataProvider;
    private readonly IMarketQuoteBroadcaster _broadcaster;
    private readonly IMarketStateEventPublisher _marketStateEventPublisher;
    private readonly MarketQuoteStreamOptions _options;
    private readonly TimeProvider _timeProvider;
    private long _sequence;

    public MockQuoteStreamPublisher(
        IMarketDataProvider configuredMarketDataProvider,
        MockMarketDataProvider mockMarketDataProvider,
        IMarketQuoteBroadcaster broadcaster,
        IMarketStateEventPublisher marketStateEventPublisher,
        IOptions<MarketQuoteStreamOptions> options,
        TimeProvider timeProvider)
    {
        _configuredMarketDataProvider = configuredMarketDataProvider;
        _mockMarketDataProvider = mockMarketDataProvider;
        _broadcaster = broadcaster;
        _marketStateEventPublisher = marketStateEventPublisher;
        _options = options.Value;
        _timeProvider = timeProvider;
    }

    public async Task<int> PublishOnceAsync(CancellationToken cancellationToken)
    {
        var sequence = Interlocked.Increment(ref _sequence);
        var query = new MarketBoardQuery(
            NormalizeSymbols(_options.Symbols),
            NormalizeToken(_options.BoardId, "G1"));
        var quotes = await ResolveSourceProvider().GetMarketBoardAsync(query, cancellationToken);
        var now = _timeProvider.GetUtcNow();
        var published = 0;

        foreach (var quote in quotes)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var update = CreateUpdate(quote, sequence, now);
            await _marketStateEventPublisher.PublishQuoteUpdateAsync(update, cancellationToken);
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

    private IMarketDataProvider ResolveSourceProvider()
    {
        return _options.SourceProvider.Equals(MarketQuoteStreamOptions.ConfiguredSourceProvider, StringComparison.OrdinalIgnoreCase)
            ? _configuredMarketDataProvider
            : _mockMarketDataProvider;
    }

    private static MarketQuoteUpdateDto CreateUpdate(
        MarketQuoteDto quote,
        long sequence,
        DateTimeOffset updatedAt)
    {
        var priceAnchor = quote.ReferencePrice > 0m ? quote.ReferencePrice : quote.LastPrice;
        var priceStep = priceAnchor >= 1_000m ? 50m : 0.05m;
        var direction = (sequence + StableSymbolOffset(quote.Symbol)) % 3 - 1;
        var lastPrice = quote.ReferencePrice > 0m
            ? quote.ReferencePrice + direction * priceStep
            : quote.LastPrice + direction * priceStep;
        lastPrice = ClampToTradingBand(lastPrice, quote.FloorPrice, quote.CeilingPrice);
        var change = quote.ReferencePrice > 0m ? lastPrice - quote.ReferencePrice : quote.Change;
        var changePercent = quote.ReferencePrice > 0m
            ? Math.Round(change / quote.ReferencePrice * 100m, 2, MidpointRounding.AwayFromZero)
            : quote.ChangePercent;
        var levelQuantityDirection = direction == 0 ? 1 : direction;
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
            BidLevels: CreatePriceLevels(
                quote.BidLevels,
                lastPrice,
                -priceStep,
                levelQuantityDirection,
                quote.FloorPrice,
                quote.CeilingPrice),
            AskLevels: CreatePriceLevels(
                quote.AskLevels,
                lastPrice,
                priceStep,
                -levelQuantityDirection,
                quote.FloorPrice,
                quote.CeilingPrice),
            TradingStatus: quote.TradingStatus,
            UpdatedAt: updatedAt);
    }

    private static IReadOnlyList<PriceLevelDto> CreatePriceLevels(
        IReadOnlyList<PriceLevelDto> currentLevels,
        decimal lastPrice,
        decimal priceStep,
        long quantityDirection,
        decimal floorPrice,
        decimal ceilingPrice)
    {
        return currentLevels
            .Take(3)
            .Select((level, index) => level with
            {
                Price = ClampToTradingBand(lastPrice + priceStep * (index + 1), floorPrice, ceilingPrice),
                Quantity = Math.Max(0, level.Quantity + quantityDirection * (index + 1) * 100)
            })
            .ToArray();
    }

    private static decimal ClampToTradingBand(decimal price, decimal floorPrice, decimal ceilingPrice)
    {
        var result = Math.Max(0m, price);

        if (floorPrice > 0m)
        {
            result = Math.Max(floorPrice, result);
        }

        if (ceilingPrice > 0m)
        {
            result = Math.Min(ceilingPrice, result);
        }

        return result;
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
