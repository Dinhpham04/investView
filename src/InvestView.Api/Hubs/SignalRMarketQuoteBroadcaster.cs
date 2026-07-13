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
        return _hubContext
            .Clients
            .Group(QuoteHubGroups.Symbol(update.BoardId, update.Symbol))
            .ReceiveQuoteUpdate(update);
    }

    public Task BroadcastTradeUpdateAsync(MarketTradeUpdateDto update, CancellationToken cancellationToken)
    {
        return _hubContext
            .Clients
            .Group(QuoteHubGroups.Symbol(update.BoardId, update.Symbol))
            .ReceiveTradeUpdate(update);
    }

    public Task BroadcastMarketIndexUpdateAsync(MarketIndexUpdateDto update, CancellationToken cancellationToken)
    {
        return _hubContext
            .Clients
            .All
            .ReceiveMarketIndexUpdate(update);
    }

    public Task BroadcastOhlcUpdateAsync(MarketOhlcUpdateDto update, CancellationToken cancellationToken)
    {
        return _hubContext
            .Clients
            .All
            .ReceiveOhlcUpdate(update);
    }

    public Task BroadcastMarketSessionUpdateAsync(MarketSessionUpdateDto update, CancellationToken cancellationToken)
    {
        return _hubContext
            .Clients
            .All
            .ReceiveMarketSessionUpdate(update);
    }

    public Task BroadcastStreamStatusAsync(QuoteStreamStatusDto status, CancellationToken cancellationToken)
    {
        return _hubContext.Clients.All.ReceiveStreamStatus(status);
    }
}
