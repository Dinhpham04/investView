namespace InvestView.Domain.Trading;

public sealed class Holding
{
    private Holding()
    {
        Symbol = string.Empty;
        BoardId = string.Empty;
    }

    public Holding(
        Guid userId,
        string symbol,
        string boardId,
        long quantity,
        long availableQuantity,
        decimal averageCost,
        DateTimeOffset? updatedAt = null)
    {
        if (userId == Guid.Empty)
        {
            throw new ArgumentException("User id is required.", nameof(userId));
        }

        if (quantity < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(quantity), "Quantity cannot be negative.");
        }

        if (availableQuantity < 0 || availableQuantity > quantity)
        {
            throw new ArgumentOutOfRangeException(nameof(availableQuantity), "Available quantity must be between zero and quantity.");
        }

        if (averageCost < 0m)
        {
            throw new ArgumentOutOfRangeException(nameof(averageCost), "Average cost cannot be negative.");
        }

        Id = Guid.NewGuid();
        UserId = userId;
        Symbol = MarketIdentity.NormalizeSymbol(symbol);
        BoardId = MarketIdentity.NormalizeBoardId(boardId);
        Quantity = quantity;
        AvailableQuantity = availableQuantity;
        AverageCost = averageCost;
        UpdatedAt = updatedAt ?? DateTimeOffset.UtcNow;
    }

    public Guid Id { get; private set; }

    public Guid UserId { get; private set; }

    public string Symbol { get; private set; }

    public string BoardId { get; private set; }

    public long Quantity { get; private set; }

    public long AvailableQuantity { get; private set; }

    public decimal AverageCost { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public UserAccount? User { get; private set; }
}
