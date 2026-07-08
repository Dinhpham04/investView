using InvestView.Application.Abstractions.Realtime;
using InvestView.Application.Dtos.Realtime;
using Microsoft.AspNetCore.SignalR;

namespace InvestView.Api.Hubs;

public sealed class QuoteHub : Hub<IQuoteClient>
{
    private readonly IMarketQuoteSubscriptionRegistry _subscriptionRegistry;

    public QuoteHub(IMarketQuoteSubscriptionRegistry subscriptionRegistry)
    {
        _subscriptionRegistry = subscriptionRegistry;
    }

    public async Task SubscribeMarketBoard(MarketBoardSubscriptionDto subscription)
    {
        var change = _subscriptionRegistry.SetConnectionSubscription(
            Context.ConnectionId,
            subscription.BoardId,
            subscription.Symbols);

        var previousGroups = change.PreviousBoardId is null
            ? []
            : change.PreviousSymbols
                .Select(symbol => QuoteHubGroups.Symbol(change.PreviousBoardId, symbol))
                .ToArray();
        var nextGroups = change.Symbols
            .Select(symbol => QuoteHubGroups.Symbol(change.BoardId, symbol))
            .ToArray();

        foreach (var groupName in previousGroups.Except(nextGroups, StringComparer.Ordinal))
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, groupName);
        }

        foreach (var groupName in nextGroups.Except(previousGroups, StringComparer.Ordinal))
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, groupName);
        }
    }

    public override Task OnDisconnectedAsync(Exception? exception)
    {
        _subscriptionRegistry.RemoveConnection(Context.ConnectionId);
        return base.OnDisconnectedAsync(exception);
    }
}
