namespace InvestView.Domain.Trading;

public sealed class OrderExecution
{
    private OrderExecution()
    {
    }

    public OrderExecution(Guid orderId, long quantity, decimal price, DateTimeOffset? executedAt = null)
    {
        if (orderId == Guid.Empty)
        {
            throw new ArgumentException("Order id is required.", nameof(orderId));
        }

        if (quantity <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(quantity), "Quantity must be positive.");
        }

        if (price <= 0m)
        {
            throw new ArgumentOutOfRangeException(nameof(price), "Price must be positive.");
        }

        Id = Guid.NewGuid();
        OrderId = orderId;
        Quantity = quantity;
        Price = price;
        GrossAmount = quantity * price;
        ExecutedAt = executedAt ?? DateTimeOffset.UtcNow;
    }

    public Guid Id { get; private set; }

    public Guid OrderId { get; private set; }

    public long Quantity { get; private set; }

    public decimal Price { get; private set; }

    public decimal GrossAmount { get; private set; }

    public DateTimeOffset ExecutedAt { get; private set; }

    public SimulatedOrder? Order { get; private set; }
}
