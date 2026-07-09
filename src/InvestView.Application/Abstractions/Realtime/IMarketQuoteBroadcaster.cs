using InvestView.Application.Dtos.MarketData;
using InvestView.Application.Dtos.Realtime;

namespace InvestView.Application.Abstractions.Realtime;

public interface IMarketQuoteBroadcaster
{
    Task BroadcastQuoteUpdateAsync(MarketQuoteUpdateDto update, CancellationToken cancellationToken);

    Task BroadcastTradeUpdateAsync(MarketTradeUpdateDto update, CancellationToken cancellationToken);

    Task BroadcastMarketIndexUpdateAsync(MarketIndexUpdateDto update, CancellationToken cancellationToken);

    Task BroadcastStreamStatusAsync(QuoteStreamStatusDto status, CancellationToken cancellationToken);
}
