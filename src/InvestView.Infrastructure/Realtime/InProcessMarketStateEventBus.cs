namespace InvestView.Infrastructure.Realtime;

public sealed class InProcessMarketStateEventBus : IMarketStateEventBus
{
    private readonly IEnumerable<IMarketStateEventHandler> _handlers;

    public InProcessMarketStateEventBus(IEnumerable<IMarketStateEventHandler> handlers)
    {
        _handlers = handlers;
    }

    public async Task PublishAsync(MarketStateEvent marketEvent, CancellationToken cancellationToken)
    {
        foreach (var handler in _handlers)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await handler.HandleAsync(marketEvent, cancellationToken);
        }
    }
}
