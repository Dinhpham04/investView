namespace InvestView.Application.Abstractions.Realtime;

public interface IMarketQuoteSubscriptionRegistry
{
    MarketQuoteConnectionSubscriptionChange SetConnectionSubscription(
        string connectionId,
        string? boardId,
        IReadOnlyCollection<string>? symbols);

    MarketQuoteSubscriptionSnapshot RemoveConnection(string connectionId);

    MarketQuoteSubscriptionSnapshot GetSnapshot();

    ValueTask<MarketQuoteSubscriptionSnapshot> WaitForChangeAsync(
        long lastObservedVersion,
        CancellationToken cancellationToken);
}

public sealed record MarketQuoteConnectionSubscriptionChange(
    string? PreviousBoardId,
    IReadOnlyList<string> PreviousSymbols,
    string BoardId,
    IReadOnlyList<string> Symbols,
    MarketQuoteSubscriptionSnapshot Snapshot);

public sealed record MarketQuoteSubscriptionSnapshot(
    IReadOnlyList<MarketQuoteBoardSubscription> Boards,
    long Version)
{
    public static MarketQuoteSubscriptionSnapshot Empty { get; } = new([], 0);
}

public sealed record MarketQuoteBoardSubscription(
    string BoardId,
    IReadOnlyList<string> Symbols);
