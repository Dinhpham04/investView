namespace InvestView.Application.Abstractions.Watchlists;

public interface IWatchlistService
{
    Task<IReadOnlyList<WatchlistItemDto>> ListAsync(
        Guid userId,
        CancellationToken cancellationToken);

    Task<AddWatchlistItemResult> AddAsync(
        Guid userId,
        string symbol,
        string boardId,
        CancellationToken cancellationToken);

    Task<bool> RemoveAsync(
        Guid userId,
        string symbol,
        string boardId,
        CancellationToken cancellationToken);
}

public sealed record WatchlistItemDto(
    Guid Id,
    string Symbol,
    string BoardId,
    DateTimeOffset CreatedAt);

public sealed record AddWatchlistItemResult(
    AddWatchlistItemStatus Status,
    WatchlistItemDto? Item);

public enum AddWatchlistItemStatus
{
    Created,
    AlreadyExists,
    InvalidInput,
    SymbolNotFound,
    UserNotFound
}
