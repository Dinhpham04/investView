namespace InvestView.Domain.Trading;

public sealed class HoldingSettlementLot
{
    private HoldingSettlementLot()
    {
        Symbol = string.Empty;
        BoardId = string.Empty;
    }

    public HoldingSettlementLot(
        Guid userId,
        string symbol,
        string boardId,
        Guid sourceOrderId,
        Guid? sourceExecutionId,
        long quantity,
        DateOnly tradeDate,
        DateOnly settlementDate,
        DateOnly availableFromDate,
        DateTimeOffset? createdAt = null)
    {
        if (userId == Guid.Empty)
        {
            throw new ArgumentException("User id is required.", nameof(userId));
        }

        if (sourceOrderId == Guid.Empty)
        {
            throw new ArgumentException("Source order id is required.", nameof(sourceOrderId));
        }

        if (sourceExecutionId == Guid.Empty)
        {
            throw new ArgumentException("Source execution id cannot be empty.", nameof(sourceExecutionId));
        }

        if (quantity <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(quantity), "Quantity must be positive.");
        }

        if (settlementDate < tradeDate)
        {
            throw new ArgumentOutOfRangeException(nameof(settlementDate), "Settlement date cannot be before trade date.");
        }

        if (availableFromDate < settlementDate)
        {
            throw new ArgumentOutOfRangeException(nameof(availableFromDate), "Available-from date cannot be before settlement date.");
        }

        Id = Guid.NewGuid();
        UserId = userId;
        Symbol = MarketIdentity.NormalizeSymbol(symbol);
        BoardId = MarketIdentity.NormalizeBoardId(boardId);
        SourceOrderId = sourceOrderId;
        SourceExecutionId = sourceExecutionId;
        Quantity = quantity;
        RemainingQuantity = quantity;
        TradeDate = tradeDate;
        SettlementDate = settlementDate;
        AvailableFromDate = availableFromDate;
        Status = HoldingSettlementLotStatus.Pending;
        CreatedAt = createdAt ?? DateTimeOffset.UtcNow;
    }

    public Guid Id { get; private set; }

    public Guid UserId { get; private set; }

    public string Symbol { get; private set; }

    public string BoardId { get; private set; }

    public Guid SourceOrderId { get; private set; }

    public Guid? SourceExecutionId { get; private set; }

    public long Quantity { get; private set; }

    public long RemainingQuantity { get; private set; }

    public DateOnly TradeDate { get; private set; }

    public DateOnly SettlementDate { get; private set; }

    public DateOnly AvailableFromDate { get; private set; }

    public HoldingSettlementLotStatus Status { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset? SettledAt { get; private set; }

    public UserAccount? User { get; private set; }

    public SimulatedOrder? SourceOrder { get; private set; }

    public void MarkSettled(DateTimeOffset? settledAt = null)
    {
        if (Status != HoldingSettlementLotStatus.Pending)
        {
            throw new InvalidOperationException("Only pending settlement lots can be settled.");
        }

        RemainingQuantity = 0;
        Status = HoldingSettlementLotStatus.Settled;
        SettledAt = settledAt ?? DateTimeOffset.UtcNow;
    }

    public void MarkFailed(DateTimeOffset? failedAt = null)
    {
        if (Status != HoldingSettlementLotStatus.Pending)
        {
            throw new InvalidOperationException("Only pending settlement lots can fail.");
        }

        Status = HoldingSettlementLotStatus.Failed;
        SettledAt = failedAt ?? DateTimeOffset.UtcNow;
    }
}

public enum HoldingSettlementLotStatus
{
    Pending = 1,
    Settled = 2,
    Cancelled = 3,
    Failed = 4
}
