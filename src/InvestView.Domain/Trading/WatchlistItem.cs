namespace InvestView.Domain.Trading;

public sealed class WatchlistItem
{
    private WatchlistItem()
    {
        Symbol = string.Empty;
        BoardId = string.Empty;
    }

    public WatchlistItem(Guid groupId, string symbol, string boardId, DateTimeOffset? createdAt = null)
    {
        if (groupId == Guid.Empty)
        {
            throw new ArgumentException("Watchlist group id is required.", nameof(groupId));
        }

        Id = Guid.NewGuid();
        GroupId = groupId;
        Symbol = MarketIdentity.NormalizeSymbol(symbol);
        BoardId = MarketIdentity.NormalizeBoardId(boardId);
        CreatedAt = createdAt ?? DateTimeOffset.UtcNow;
    }

    public Guid Id { get; private set; }

    public Guid GroupId { get; private set; }

    public string Symbol { get; private set; }

    public string BoardId { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public WatchlistGroup? Group { get; private set; }
}
