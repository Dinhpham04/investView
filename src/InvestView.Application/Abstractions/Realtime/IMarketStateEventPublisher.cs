using InvestView.Application.Dtos.MarketData;

namespace InvestView.Application.Abstractions.Realtime;

public interface IMarketStateEventPublisher
{
    Task PublishQuoteUpdateAsync(MarketQuoteUpdateDto update, CancellationToken cancellationToken);

    Task PublishTradeUpdateAsync(MarketTradeUpdateDto update, CancellationToken cancellationToken);

    Task PublishMarketIndexUpdateAsync(MarketIndexUpdateDto update, CancellationToken cancellationToken);

    Task PublishOhlcUpdateAsync(MarketOhlcUpdateDto update, CancellationToken cancellationToken);

    Task PublishMarketSessionUpdateAsync(MarketSessionUpdateDto update, CancellationToken cancellationToken);
}
