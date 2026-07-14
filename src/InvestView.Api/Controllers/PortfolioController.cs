using System.Security.Claims;
using InvestView.Application.Abstractions.Portfolio;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InvestView.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/portfolio")]
[Produces("application/json")]
public sealed class PortfolioController : ControllerBase
{
    private readonly IPortfolioService _portfolioService;

    public PortfolioController(IPortfolioService portfolioService)
    {
        _portfolioService = portfolioService;
    }

    [HttpGet]
    [ProducesResponseType<PortfolioSnapshotResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PortfolioSnapshotResponse>> Get(CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId))
        {
            return Unauthorized();
        }

        var snapshot = await _portfolioService.GetSnapshotAsync(userId, cancellationToken);
        if (snapshot is null)
        {
            return NotFound(new ProblemDetails { Title = "Portfolio was not found." });
        }

        return Ok(ToResponse(snapshot));
    }

    [HttpGet("holdings")]
    [ProducesResponseType<PortfolioHoldingsSnapshotResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PortfolioHoldingsSnapshotResponse>> GetHoldings(CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId))
        {
            return Unauthorized();
        }

        var snapshot = await _portfolioService.GetHoldingsAsync(userId, cancellationToken);
        if (snapshot is null)
        {
            return NotFound(new ProblemDetails { Title = "Portfolio was not found." });
        }

        return Ok(ToHoldingsResponse(snapshot));
    }

    private bool TryGetUserId(out Guid userId)
    {
        var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(userIdClaim, out userId);
    }

    private static PortfolioSnapshotResponse ToResponse(PortfolioSnapshotDto snapshot)
    {
        return new PortfolioSnapshotResponse(
            snapshot.CashAccounts
                .Select(account => new CashAccountResponse(
                    account.Currency,
                    account.Balance,
                    account.AvailableBalance,
                    account.UpdatedAt))
                .ToArray(),
            snapshot.Holdings
                .Select(holding => new HoldingPositionResponse(
                    holding.Symbol,
                    holding.BoardId,
                    holding.Quantity,
                    holding.AvailableQuantity,
                    holding.PendingReceiveQuantity,
                    holding.AverageCost,
                    holding.LastPrice,
                    holding.MarketValue,
                    holding.CostValue,
                    holding.UnrealizedPnL,
                    holding.UpdatedAt))
                .ToArray(),
            snapshot.TotalCash,
            snapshot.TotalAvailableCash,
            snapshot.TotalMarketValue,
            snapshot.TotalEquity,
            snapshot.TotalUnrealizedPnL,
            snapshot.UpdatedAt);
    }

    private static PortfolioHoldingsSnapshotResponse ToHoldingsResponse(PortfolioHoldingsSnapshotDto snapshot)
    {
        return new PortfolioHoldingsSnapshotResponse(
            snapshot.Holdings
                .Select(holding => new PortfolioHoldingResponse(
                    holding.Symbol,
                    holding.BoardId,
                    holding.Quantity,
                    holding.AvailableQuantity,
                    holding.PendingReceiveQuantity,
                    holding.PendingT0Quantity,
                    holding.PendingT1Quantity,
                    holding.PendingT2Quantity,
                    holding.NextAvailableDate,
                    holding.AverageCost,
                    holding.LastPrice,
                    holding.CostValue,
                    holding.MarketValue,
                    holding.UnrealizedPnL,
                    holding.UnrealizedPnLPercent,
                    holding.UpdatedAt))
                .ToArray(),
            snapshot.TotalQuantity,
            snapshot.TotalAvailableQuantity,
            snapshot.TotalPendingReceiveQuantity,
            snapshot.TotalCostValue,
            snapshot.TotalMarketValue,
            snapshot.TotalUnrealizedPnL,
            snapshot.UpdatedAt);
    }
}

public sealed record PortfolioSnapshotResponse(
    IReadOnlyList<CashAccountResponse> CashAccounts,
    IReadOnlyList<HoldingPositionResponse> Holdings,
    decimal TotalCash,
    decimal TotalAvailableCash,
    decimal TotalMarketValue,
    decimal TotalEquity,
    decimal TotalUnrealizedPnL,
    DateTimeOffset UpdatedAt);

public sealed record CashAccountResponse(
    string Currency,
    decimal Balance,
    decimal AvailableBalance,
    DateTimeOffset UpdatedAt);

public sealed record HoldingPositionResponse(
    string Symbol,
    string BoardId,
    long Quantity,
    long AvailableQuantity,
    long PendingReceiveQuantity,
    decimal AverageCost,
    decimal LastPrice,
    decimal MarketValue,
    decimal CostValue,
    decimal UnrealizedPnL,
    DateTimeOffset UpdatedAt);

public sealed record PortfolioHoldingsSnapshotResponse(
    IReadOnlyList<PortfolioHoldingResponse> Holdings,
    long TotalQuantity,
    long TotalAvailableQuantity,
    long TotalPendingReceiveQuantity,
    decimal TotalCostValue,
    decimal TotalMarketValue,
    decimal TotalUnrealizedPnL,
    DateTimeOffset UpdatedAt);

public sealed record PortfolioHoldingResponse(
    string Symbol,
    string BoardId,
    long Quantity,
    long AvailableQuantity,
    long PendingReceiveQuantity,
    long PendingT0Quantity,
    long PendingT1Quantity,
    long PendingT2Quantity,
    DateOnly? NextAvailableDate,
    decimal AverageCost,
    decimal LastPrice,
    decimal CostValue,
    decimal MarketValue,
    decimal UnrealizedPnL,
    decimal UnrealizedPnLPercent,
    DateTimeOffset UpdatedAt);
