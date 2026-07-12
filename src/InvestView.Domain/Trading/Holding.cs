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

    public void ApplyBuy(long quantity, decimal price, DateTimeOffset? updatedAt = null)
    {
        if (quantity <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(quantity), "Quantity must be positive.");
        }

        if (price <= 0m)
        {
            throw new ArgumentOutOfRangeException(nameof(price), "Price must be positive.");
        }

        var currentCost = Quantity * AverageCost;
        var addedCost = quantity * price;
        var totalQuantity = Quantity + quantity;

        Quantity = totalQuantity;
        AvailableQuantity += quantity;
        AverageCost = totalQuantity == 0 ? 0m : (currentCost + addedCost) / totalQuantity;
        UpdatedAt = updatedAt ?? DateTimeOffset.UtcNow;
    }

    public void ApplySell(long quantity, DateTimeOffset? updatedAt = null)
    {
        if (quantity <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(quantity), "Quantity must be positive.");
        }

        if (quantity > AvailableQuantity)
        {
            throw new InvalidOperationException("Available holding quantity is insufficient.");
        }

        Quantity -= quantity;
        AvailableQuantity -= quantity;
        if (Quantity == 0)
        {
            AverageCost = 0m;
        }

        UpdatedAt = updatedAt ?? DateTimeOffset.UtcNow;
    }
}
