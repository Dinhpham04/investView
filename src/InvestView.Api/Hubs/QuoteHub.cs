using Microsoft.AspNetCore.SignalR;

namespace InvestView.Api.Hubs;

public sealed class QuoteHub : Hub<IQuoteClient>
{
}
