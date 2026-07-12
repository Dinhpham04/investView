using System.Security.Claims;
using InvestView.Application.Abstractions.Watchlists;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InvestView.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/watchlist")]
[Produces("application/json")]
public sealed class WatchlistController : ControllerBase
{
    private readonly IWatchlistService _watchlistService;

    public WatchlistController(IWatchlistService watchlistService)
    {
        _watchlistService = watchlistService;
    }

    [HttpGet]
    [ProducesResponseType<IReadOnlyList<WatchlistItemResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<IReadOnlyList<WatchlistItemResponse>>> Get(CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId))
        {
            return Unauthorized();
        }

        var items = await _watchlistService.ListAsync(userId, cancellationToken);
        return Ok(items.Select(ToResponse).ToArray());
    }

    [HttpPost]
    [ProducesResponseType<WatchlistItemResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<WatchlistItemResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<WatchlistItemResponse>> Add(
        [FromBody] AddWatchlistItemRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId))
        {
            return Unauthorized();
        }

        var result = await _watchlistService.AddAsync(
            userId,
            request.Symbol,
            request.BoardId,
            cancellationToken);

        return result.Status switch
        {
            AddWatchlistItemStatus.Created when result.Item is not null =>
                Created("/api/watchlist", ToResponse(result.Item)),
            AddWatchlistItemStatus.AlreadyExists when result.Item is not null =>
                Ok(ToResponse(result.Item)),
            AddWatchlistItemStatus.InvalidInput =>
                BadRequest(new ProblemDetails { Title = "Invalid watchlist item." }),
            AddWatchlistItemStatus.SymbolNotFound =>
                NotFound(new ProblemDetails { Title = "Symbol was not found." }),
            AddWatchlistItemStatus.UserNotFound =>
                NotFound(new ProblemDetails { Title = "User was not found." }),
            _ => StatusCode(StatusCodes.Status500InternalServerError)
        };
    }

    [HttpDelete("{boardId}/{symbol}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Remove(
        string boardId,
        string symbol,
        CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId))
        {
            return Unauthorized();
        }

        await _watchlistService.RemoveAsync(userId, symbol, boardId, cancellationToken);
        return NoContent();
    }

    private bool TryGetUserId(out Guid userId)
    {
        var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(userIdClaim, out userId);
    }

    private static WatchlistItemResponse ToResponse(WatchlistItemDto item)
    {
        return new WatchlistItemResponse(
            item.Id,
            item.Symbol,
            item.BoardId,
            item.CreatedAt);
    }
}

public sealed record AddWatchlistItemRequest(string Symbol, string BoardId);

public sealed record WatchlistItemResponse(
    Guid Id,
    string Symbol,
    string BoardId,
    DateTimeOffset CreatedAt);
