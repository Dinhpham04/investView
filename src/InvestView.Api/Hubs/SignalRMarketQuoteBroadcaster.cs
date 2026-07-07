using InvestView.Application.Abstractions.Realtime;
using InvestView.Application.Dtos.MarketData;
using InvestView.Application.Dtos.Realtime;
using Microsoft.AspNetCore.SignalR;

namespace InvestView.Api.Hubs;

public sealed class SignalRMarketQuoteBroadcaster : IMarketQuoteBroadcaster
{
    private readonly IHubContext<QuoteHub, IQuoteClient> _hubContext;

    public SignalRMarketQuoteBroadcaster(IHubContext<QuoteHub, IQuoteClient> hubContext)
    {
        _hubContext = hubContext;
    }

    public Task BroadcastQuoteUpdateAsync(MarketQuoteUpdateDto update, CancellationToken cancellationToken)
    {
        return _hubContext.Clients.All.ReceiveQuoteUpdate(update);
    }

    public Task BroadcastStreamStatusAsync(QuoteStreamStatusDto status, CancellationToken cancellationToken)
    {
        return _hubContext.Clients.All.ReceiveStreamStatus(status);
    }
}