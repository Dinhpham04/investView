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
    [ProducesResponseType<IReadOnlyList<WatchlistGroupResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<IReadOnlyList<WatchlistGroupResponse>>> Get(CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId))
        {
            return Unauthorized();
        }

        var groups = await _watchlistService.ListAsync(userId, cancellationToken);
        return Ok(groups.Select(ToResponse).ToArray());
    }

    [HttpPost]
    [ProducesResponseType<WatchlistGroupResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<WatchlistGroupResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<WatchlistGroupResponse>> CreateGroup(
        [FromBody] CreateWatchlistGroupRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId))
        {
            return Unauthorized();
        }

        var result = await _watchlistService.CreateGroupAsync(userId, request.Name, cancellationToken);

        return result.Status switch
        {
            CreateWatchlistGroupStatus.Created when result.Group is not null =>
                Created($"/api/watchlist/{result.Group.Id}", ToResponse(result.Group)),
            CreateWatchlistGroupStatus.AlreadyExists when result.Group is not null =>
                Ok(ToResponse(result.Group)),
            CreateWatchlistGroupStatus.InvalidInput =>
                BadRequest(new ProblemDetails { Title = "Invalid watchlist group." }),
            CreateWatchlistGroupStatus.UserNotFound =>
                NotFound(new ProblemDetails { Title = "User was not found." }),
            _ => StatusCode(StatusCodes.Status500InternalServerError)
        };
    }

    [HttpPost("{groupId:guid}/items")]
    [ProducesResponseType<WatchlistItemResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<WatchlistItemResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<WatchlistItemResponse>> AddItem(
        Guid groupId,
        [FromBody] AddWatchlistItemRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId))
        {
            return Unauthorized();
        }

        var result = await _watchlistService.AddItemAsync(
            userId,
            groupId,
            request.Symbol,
            request.BoardId,
            cancellationToken);

        return result.Status switch
        {
            AddWatchlistItemStatus.Created when result.Item is not null =>
                Created($"/api/watchlist/{groupId}/items", ToResponse(result.Item)),
            AddWatchlistItemStatus.AlreadyExists when result.Item is not null =>
                Ok(ToResponse(result.Item)),
            AddWatchlistItemStatus.InvalidInput =>
                BadRequest(new ProblemDetails { Title = "Invalid watchlist item." }),
            AddWatchlistItemStatus.GroupNotFound =>
                NotFound(new ProblemDetails { Title = "Watchlist group was not found." }),
            AddWatchlistItemStatus.SymbolNotFound =>
                NotFound(new ProblemDetails { Title = "Symbol was not found." }),
            AddWatchlistItemStatus.UserNotFound =>
                NotFound(new ProblemDetails { Title = "User was not found." }),
            _ => StatusCode(StatusCodes.Status500InternalServerError)
        };
    }

    [HttpDelete("{groupId:guid}/items/{boardId}/{symbol}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> RemoveItem(
        Guid groupId,
        string boardId,
        string symbol,
        CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId))
        {
            return Unauthorized();
        }

        await _watchlistService.RemoveItemAsync(userId, groupId, symbol, boardId, cancellationToken);
        return NoContent();
    }

    private bool TryGetUserId(out Guid userId)
    {
        var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(userIdClaim, out userId);
    }

    private static WatchlistGroupResponse ToResponse(WatchlistGroupDto group)
    {
        return new WatchlistGroupResponse(
            group.Id,
            group.Name,
            group.CreatedAt,
            group.UpdatedAt,
            group.Items.Select(ToResponse).ToArray());
    }

    private static WatchlistItemResponse ToResponse(WatchlistItemDto item)
    {
        return new WatchlistItemResponse(
            item.Id,
            item.GroupId,
            item.Symbol,
            item.BoardId,
            item.CreatedAt);
    }
}

public sealed record CreateWatchlistGroupRequest(string Name);

public sealed record AddWatchlistItemRequest(string Symbol, string BoardId);

public sealed record WatchlistGroupResponse(
    Guid Id,
    string Name,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    IReadOnlyList<WatchlistItemResponse> Items);

public sealed record WatchlistItemResponse(
    Guid Id,
    Guid GroupId,
    string Symbol,
    string BoardId,
    DateTimeOffset CreatedAt);
