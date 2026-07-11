namespace InvestView.Domain.Trading;

public sealed class SimulatedOrder
{
    private readonly List<OrderExecution> _executions = [];

    private SimulatedOrder()
    {
        Symbol = string.Empty;
        BoardId = string.Empty;
    }

    public SimulatedOrder(
        Guid userId,
        string symbol,
        string boardId,
        OrderSide side,
        long quantity,
        decimal? limitPrice,
        DateTimeOffset? createdAt = null)
    {
        if (userId == Guid.Empty)
        {
            throw new ArgumentException("User id is required.", nameof(userId));
        }

        if (quantity <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(quantity), "Quantity must be positive.");
        }

        if (!Enum.IsDefined(side))
        {
            throw new ArgumentOutOfRangeException(nameof(side), "Order side is invalid.");
        }

        if (limitPrice is < 0m)
        {
            throw new ArgumentOutOfRangeException(nameof(limitPrice), "Limit price cannot be negative.");
        }

        Id = Guid.NewGuid();
        UserId = userId;
        Symbol = MarketIdentity.NormalizeSymbol(symbol);
        BoardId = MarketIdentity.NormalizeBoardId(boardId);
        Side = side;
        Quantity = quantity;
        LimitPrice = limitPrice;
        Status = OrderStatus.New;
        CreatedAt = createdAt ?? DateTimeOffset.UtcNow;
        UpdatedAt = CreatedAt;
    }

    public Guid Id { get; private set; }

    public Guid UserId { get; private set; }

    public string Symbol { get; private set; }

    public string BoardId { get; private set; }

    public OrderSide Side { get; private set; }

    public long Quantity { get; private set; }

    public decimal? LimitPrice { get; private set; }

    public OrderStatus Status { get; private set; }

    public long FilledQuantity { get; private set; }

    public decimal? AverageFillPrice { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public UserAccount? User { get; private set; }

    public IReadOnlyCollection<OrderExecution> Executions => _executions;
}

public enum OrderSide
{
    Buy = 1,
    Sell = 2
}

public enum OrderStatus
{
    New = 1,
    Filled = 2,
    Cancelled = 3,
    Rejected = 4
}
