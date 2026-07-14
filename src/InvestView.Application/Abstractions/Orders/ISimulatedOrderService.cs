using InvestView.Domain.Trading;

namespace InvestView.Application.Abstractions.Orders;

public interface ISimulatedOrderService
{
    Task<IReadOnlyList<SimulatedOrderDto>> ListAsync(
        Guid userId,
        CancellationToken cancellationToken);

    Task<PlaceSimulatedOrderResult> PlaceAsync(
        Guid userId,
        PlaceSimulatedOrderCommand command,
        CancellationToken cancellationToken);

    Task<CancelSimulatedOrderResult> CancelAsync(
        Guid userId,
        Guid orderId,
        CancellationToken cancellationToken);
}

public sealed record PlaceSimulatedOrderCommand(
    string Symbol,
    string BoardId,
    OrderSide Side,
    OrderType OrderType,
    long Quantity,
    decimal? LimitPrice);

public sealed record PlaceSimulatedOrderResult(
    PlaceSimulatedOrderStatus Status,
    SimulatedOrderDto? Order);

public enum PlaceSimulatedOrderStatus
{
    Created,
    InvalidInput,
    UserNotFound,
    SymbolNotFound,
    PriceUnavailable,
    InsufficientCash,
    InsufficientHolding,
    MarketClosed
}

public sealed record CancelSimulatedOrderResult(
    CancelSimulatedOrderStatus Status,
    SimulatedOrderDto? Order);

public enum CancelSimulatedOrderStatus
{
    Cancelled,
    NotFound,
    CannotCancel
}

public sealed record SimulatedOrderDto(
    Guid Id,
    string Symbol,
    string BoardId,
    OrderSide Side,
    OrderType OrderType,
    long Quantity,
    decimal? LimitPrice,
    OrderStatus Status,
    long FilledQuantity,
    decimal? AverageFillPrice,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    IReadOnlyList<OrderExecutionDto> Executions);

public sealed record OrderExecutionDto(
    Guid Id,
    long Quantity,
    decimal Price,
    decimal GrossAmount,
    DateTimeOffset ExecutedAt);
