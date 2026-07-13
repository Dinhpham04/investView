namespace InvestView.Application.Abstractions.Watchlists;

public interface IWatchlistService
{
    Task<IReadOnlyList<WatchlistGroupDto>> ListAsync(
        Guid userId,
        CancellationToken cancellationToken);

    Task<CreateWatchlistGroupResult> CreateGroupAsync(
        Guid userId,
        string name,
        CancellationToken cancellationToken);

    Task<AddWatchlistItemResult> AddItemAsync(
        Guid userId,
        Guid groupId,
        string symbol,
        string boardId,
        CancellationToken cancellationToken);

    Task<bool> RemoveItemAsync(
        Guid userId,
        Guid groupId,
        string symbol,
        string boardId,
        CancellationToken cancellationToken);
}

public sealed record WatchlistGroupDto(
    Guid Id,
    string Name,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    IReadOnlyList<WatchlistItemDto> Items);

public sealed record WatchlistItemDto(
    Guid Id,
    Guid GroupId,
    string Symbol,
    string BoardId,
    DateTimeOffset CreatedAt);

public sealed record CreateWatchlistGroupResult(
    CreateWatchlistGroupStatus Status,
    WatchlistGroupDto? Group);

public sealed record AddWatchlistItemResult(
    AddWatchlistItemStatus Status,
    WatchlistItemDto? Item);

public enum CreateWatchlistGroupStatus
{
    Created,
    AlreadyExists,
    InvalidInput,
    UserNotFound
}

public enum AddWatchlistItemStatus
{
    Created,
    AlreadyExists,
    InvalidInput,
    GroupNotFound,
    SymbolNotFound,
    UserNotFound
}
