using InvestView.Application.Dtos.MarketData;

namespace InvestView.Infrastructure.Realtime;

public sealed record MarketStateEvent(
    MarketStateEventKind Kind,
    MarketQuoteUpdateDto? QuoteUpdate = null,
    MarketTradeUpdateDto? TradeUpdate = null,
    MarketIndexUpdateDto? MarketIndexUpdate = null);

public enum MarketStateEventKind
{
    QuoteUpdate,
    TradeUpdate,
    MarketIndexUpdate
}

public interface IMarketStateEventBus
{
    Task PublishAsync(MarketStateEvent marketEvent, CancellationToken cancellationToken);
}

public interface IMarketStateEventHandler
{
    Task HandleAsync(MarketStateEvent marketEvent, CancellationToken cancellationToken);
}
