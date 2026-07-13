using InvestView.Application.Dtos.MarketData;
using InvestView.Application.Dtos.Realtime;

namespace InvestView.Api.Hubs;

public interface IQuoteClient
{
    Task ReceiveQuoteUpdate(MarketQuoteUpdateDto update);

    Task ReceiveTradeUpdate(MarketTradeUpdateDto update);

    Task ReceiveMarketIndexUpdate(MarketIndexUpdateDto update);

    Task ReceiveOhlcUpdate(MarketOhlcUpdateDto update);

    Task ReceiveMarketSessionUpdate(MarketSessionUpdateDto update);

    Task ReceiveStreamStatus(QuoteStreamStatusDto status);
}
