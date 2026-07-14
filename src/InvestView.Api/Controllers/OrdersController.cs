using System.Security.Claims;
using InvestView.Application.Abstractions.Orders;
using InvestView.Domain.Trading;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InvestView.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/orders")]
[Produces("application/json")]
public sealed class OrdersController : ControllerBase
{
    private readonly ISimulatedOrderService _orderService;

    public OrdersController(ISimulatedOrderService orderService)
    {
        _orderService = orderService;
    }

    [HttpGet]
    [ProducesResponseType<IReadOnlyList<SimulatedOrderResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<IReadOnlyList<SimulatedOrderResponse>>> List(CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId))
        {
            return Unauthorized();
        }

        var orders = await _orderService.ListAsync(userId, cancellationToken);
        return Ok(orders.Select(ToResponse).ToArray());
    }

    [HttpPost]
    [ProducesResponseType<SimulatedOrderResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<SimulatedOrderResponse>> Place(
        [FromBody] PlaceOrderRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId))
        {
            return Unauthorized();
        }

        if (!Enum.TryParse<OrderSide>(request.Side, ignoreCase: true, out var side) ||
            !Enum.IsDefined(side))
        {
            return BadRequest(new ProblemDetails { Title = "Invalid order side." });
        }

        var requestedOrderType = TryGetOrderType(request, out var orderType)
            ? orderType
            : (OrderType?)null;
        if (requestedOrderType is null)
        {
            return BadRequest(new ProblemDetails { Title = "Invalid order type." });
        }

        var result = await _orderService.PlaceAsync(
            userId,
            new PlaceSimulatedOrderCommand(
                request.Symbol,
                request.BoardId,
                side,
                requestedOrderType.Value,
                request.Quantity,
                request.LimitPrice),
            cancellationToken);

        return result.Status switch
        {
            PlaceSimulatedOrderStatus.Created when result.Order is not null =>
                Created($"/api/orders/{result.Order.Id}", ToResponse(result.Order)),
            PlaceSimulatedOrderStatus.InvalidInput =>
                BadRequest(new ProblemDetails { Title = "Invalid simulated order." }),
            PlaceSimulatedOrderStatus.InsufficientCash =>
                BadRequest(new ProblemDetails { Title = "Insufficient simulated cash." }),
            PlaceSimulatedOrderStatus.InsufficientHolding =>
                BadRequest(new ProblemDetails { Title = "Insufficient simulated holding." }),
            PlaceSimulatedOrderStatus.MarketClosed =>
                BadRequest(new ProblemDetails { Title = "Market is not open for simulated orders." }),
            PlaceSimulatedOrderStatus.SymbolNotFound =>
                NotFound(new ProblemDetails { Title = "Symbol was not found." }),
            PlaceSimulatedOrderStatus.UserNotFound =>
                NotFound(new ProblemDetails { Title = "User was not found." }),
            PlaceSimulatedOrderStatus.PriceUnavailable =>
                Conflict(new ProblemDetails { Title = "Market price is unavailable." }),
            _ => StatusCode(StatusCodes.Status500InternalServerError)
        };
    }

    [HttpPost("{orderId:guid}/cancel")]
    [ProducesResponseType<SimulatedOrderResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<SimulatedOrderResponse>> Cancel(
        Guid orderId,
        CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId))
        {
            return Unauthorized();
        }

        var result = await _orderService.CancelAsync(userId, orderId, cancellationToken);
        return result.Status switch
        {
            CancelSimulatedOrderStatus.Cancelled when result.Order is not null =>
                Ok(ToResponse(result.Order)),
            CancelSimulatedOrderStatus.NotFound =>
                NotFound(new ProblemDetails { Title = "Order was not found." }),
            CancelSimulatedOrderStatus.CannotCancel =>
                Conflict(new ProblemDetails { Title = "Order cannot be cancelled." }),
            _ => StatusCode(StatusCodes.Status500InternalServerError)
        };
    }

    private bool TryGetUserId(out Guid userId)
    {
        var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(userIdClaim, out userId);
    }

    private static bool TryGetOrderType(PlaceOrderRequest request, out OrderType orderType)
    {
        if (string.IsNullOrWhiteSpace(request.OrderType))
        {
            orderType = request.LimitPrice is null ? OrderType.MTL : OrderType.LO;
            return true;
        }

        return Enum.TryParse(request.OrderType, ignoreCase: true, out orderType) &&
               Enum.IsDefined(orderType);
    }

    private static SimulatedOrderResponse ToResponse(SimulatedOrderDto order)
    {
        return new SimulatedOrderResponse(
            order.Id,
            order.Symbol,
            order.BoardId,
            order.Side.ToString(),
            order.OrderType.ToString(),
            order.Quantity,
            order.LimitPrice,
            order.Status.ToString(),
            order.FilledQuantity,
            order.AverageFillPrice,
            order.CreatedAt,
            order.UpdatedAt,
            order.Executions
                .Select(execution => new OrderExecutionResponse(
                    execution.Id,
                    execution.Quantity,
                    execution.Price,
                    execution.GrossAmount,
                    execution.ExecutedAt))
                .ToArray());
    }
}

public sealed record PlaceOrderRequest(
    string Symbol,
    string BoardId,
    string Side,
    string? OrderType,
    long Quantity,
    decimal? LimitPrice);

public sealed record SimulatedOrderResponse(
    Guid Id,
    string Symbol,
    string BoardId,
    string Side,
    string OrderType,
    long Quantity,
    decimal? LimitPrice,
    string Status,
    long FilledQuantity,
    decimal? AverageFillPrice,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    IReadOnlyList<OrderExecutionResponse> Executions);

public sealed record OrderExecutionResponse(
    Guid Id,
    long Quantity,
    decimal Price,
    decimal GrossAmount,
    DateTimeOffset ExecutedAt);
