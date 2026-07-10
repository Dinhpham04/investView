using InvestView.Application.Abstractions.MarketData;
using InvestView.Application.Abstractions.Realtime;
using InvestView.Application.Dtos.MarketData;

namespace InvestView.Infrastructure.Realtime;

public sealed class MarketStateEventPublisher : IMarketStateEventPublisher
{
    private readonly IMarketStateStore _sharedStateStore;
    private readonly IMarketStateEventBus _eventBus;

    public MarketStateEventPublisher(
        IMarketStateStore sharedStateStore,
        IMarketStateEventBus eventBus)
    {
        _sharedStateStore = sharedStateStore;
        _eventBus = eventBus;
    }

    public async Task PublishQuoteUpdateAsync(MarketQuoteUpdateDto update, CancellationToken cancellationToken)
    {
        var mergedUpdate = await _sharedStateStore.ApplyQuoteUpdateAsync(update, cancellationToken);
        await _eventBus.PublishAsync(new MarketStateEvent(MarketStateEventKind.QuoteUpdate, QuoteUpdate: mergedUpdate), cancellationToken);
    }

    public async Task PublishTradeUpdateAsync(MarketTradeUpdateDto update, CancellationToken cancellationToken)
    {
        var mergedUpdate = await _sharedStateStore.ApplyTradeUpdateAsync(update, cancellationToken);
        await _eventBus.PublishAsync(new MarketStateEvent(MarketStateEventKind.TradeUpdate, TradeUpdate: mergedUpdate), cancellationToken);
    }

    public async Task PublishMarketIndexUpdateAsync(MarketIndexUpdateDto update, CancellationToken cancellationToken)
    {
        var mergedUpdate = await _sharedStateStore.ApplyMarketIndexUpdateAsync(update, cancellationToken);
        await _eventBus.PublishAsync(new MarketStateEvent(MarketStateEventKind.MarketIndexUpdate, MarketIndexUpdate: mergedUpdate), cancellationToken);
    }

    public async Task PublishOhlcUpdateAsync(MarketOhlcUpdateDto update, CancellationToken cancellationToken)
    {
        var mergedUpdate = await _sharedStateStore.ApplyOhlcUpdateAsync(update, cancellationToken);
        await _eventBus.PublishAsync(new MarketStateEvent(MarketStateEventKind.OhlcUpdate, OhlcUpdate: mergedUpdate), cancellationToken);
    }

    public async Task PublishMarketSessionUpdateAsync(MarketSessionUpdateDto update, CancellationToken cancellationToken)
    {
        var mergedUpdate = await _sharedStateStore.ApplyMarketSessionUpdateAsync(update, cancellationToken);
        await _eventBus.PublishAsync(new MarketStateEvent(MarketStateEventKind.MarketSessionUpdate, MarketSessionUpdate: mergedUpdate), cancellationToken);
    }
}
