using InvestView.Application.Dtos.MarketData;

namespace InvestView.Infrastructure.Realtime;

public sealed record MarketStateEvent(
    MarketStateEventKind Kind,
    MarketQuoteUpdateDto? QuoteUpdate = null,
    MarketTradeUpdateDto? TradeUpdate = null,
    MarketIndexUpdateDto? MarketIndexUpdate = null,
    MarketOhlcUpdateDto? OhlcUpdate = null,
    MarketSessionUpdateDto? MarketSessionUpdate = null);

public enum MarketStateEventKind
{
    QuoteUpdate,
    TradeUpdate,
    MarketIndexUpdate,
    OhlcUpdate,
    MarketSessionUpdate
}

public interface IMarketStateEventBus
{
    Task PublishAsync(MarketStateEvent marketEvent, CancellationToken cancellationToken);
}

public interface IMarketStateEventHandler
{
    Task HandleAsync(MarketStateEvent marketEvent, CancellationToken cancellationToken);
}
