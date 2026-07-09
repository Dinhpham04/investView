using InvestView.Application.Dtos.MarketData;
using InvestView.Application.Dtos.Realtime;

namespace InvestView.Api.Hubs;

public interface IQuoteClient
{
    Task ReceiveQuoteUpdate(MarketQuoteUpdateDto update);

    Task ReceiveTradeUpdate(MarketTradeUpdateDto update);

    Task ReceiveStreamStatus(QuoteStreamStatusDto status);
}
