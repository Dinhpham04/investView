namespace InvestView.Domain.Trading;

public sealed class WatchlistItem
{
    private WatchlistItem()
    {
        Symbol = string.Empty;
        BoardId = string.Empty;
    }

    public WatchlistItem(Guid userId, string symbol, string boardId, DateTimeOffset? createdAt = null)
    {
        if (userId == Guid.Empty)
        {
            throw new ArgumentException("User id is required.", nameof(userId));
        }

        Id = Guid.NewGuid();
        UserId = userId;
        Symbol = MarketIdentity.NormalizeSymbol(symbol);
        BoardId = MarketIdentity.NormalizeBoardId(boardId);
        CreatedAt = createdAt ?? DateTimeOffset.UtcNow;
    }

    public Guid Id { get; private set; }

    public Guid UserId { get; private set; }

    public string Symbol { get; private set; }

    public string BoardId { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public UserAccount? User { get; private set; }
}
