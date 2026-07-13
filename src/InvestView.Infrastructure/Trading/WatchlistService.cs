using InvestView.Application.Abstractions.MarketData;
using InvestView.Application.Abstractions.Watchlists;
using InvestView.Domain.Trading;
using InvestView.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace InvestView.Infrastructure.Trading;

public sealed class WatchlistService : IWatchlistService
{
    private readonly InvestViewDbContext _dbContext;
    private readonly IMarketDataProvider _marketDataProvider;
    private readonly TimeProvider _timeProvider;

    public WatchlistService(
        InvestViewDbContext dbContext,
        IMarketDataProvider marketDataProvider,
        TimeProvider timeProvider)
    {
        _dbContext = dbContext;
        _marketDataProvider = marketDataProvider;
        _timeProvider = timeProvider;
    }

    public async Task<IReadOnlyList<WatchlistGroupDto>> ListAsync(
        Guid userId,
        CancellationToken cancellationToken)
    {
        if (userId == Guid.Empty)
        {
            return [];
        }

        var groups = await _dbContext.WatchlistGroups
            .AsNoTracking()
            .Include(group => group.Items)
            .Where(group => group.UserId == userId)
            .OrderBy(group => group.CreatedAt)
            .ThenBy(group => group.Name)
            .ToArrayAsync(cancellationToken);

        return groups.Select(ToDto).ToArray();
    }

    public async Task<CreateWatchlistGroupResult> CreateGroupAsync(
        Guid userId,
        string name,
        CancellationToken cancellationToken)
    {
        if (!TryNormalizeName(name, out var normalizedName))
        {
            return new CreateWatchlistGroupResult(CreateWatchlistGroupStatus.InvalidInput, null);
        }

        if (userId == Guid.Empty ||
            !await _dbContext.Users.AnyAsync(user => user.Id == userId, cancellationToken))
        {
            return new CreateWatchlistGroupResult(CreateWatchlistGroupStatus.UserNotFound, null);
        }

        var existingGroup = await FindGroupByNameAsync(
            userId,
            normalizedName,
            track: false,
            cancellationToken);
        if (existingGroup is not null)
        {
            return new CreateWatchlistGroupResult(CreateWatchlistGroupStatus.AlreadyExists, ToDto(existingGroup));
        }

        var group = new WatchlistGroup(userId, normalizedName, _timeProvider.GetUtcNow());
        _dbContext.WatchlistGroups.Add(group);

        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            var racedGroup = await FindGroupByNameAsync(
                userId,
                normalizedName,
                track: false,
                cancellationToken);
            if (racedGroup is not null)
            {
                _dbContext.Entry(group).State = EntityState.Detached;
                return new CreateWatchlistGroupResult(CreateWatchlistGroupStatus.AlreadyExists, ToDto(racedGroup));
            }

            throw;
        }

        return new CreateWatchlistGroupResult(CreateWatchlistGroupStatus.Created, ToDto(group));
    }

    public async Task<AddWatchlistItemResult> AddItemAsync(
        Guid userId,
        Guid groupId,
        string symbol,
        string boardId,
        CancellationToken cancellationToken)
    {
        if (userId == Guid.Empty)
        {
            return new AddWatchlistItemResult(AddWatchlistItemStatus.UserNotFound, null);
        }

        if (!TryNormalize(symbol, boardId, out var normalizedSymbol, out var normalizedBoardId))
        {
            return new AddWatchlistItemResult(AddWatchlistItemStatus.InvalidInput, null);
        }

        var group = await FindGroupByIdAsync(
            userId,
            groupId,
            track: false,
            cancellationToken);
        if (group is null)
        {
            return new AddWatchlistItemResult(AddWatchlistItemStatus.GroupNotFound, null);
        }

        var existingItem = await FindItemAsync(
            groupId,
            normalizedSymbol,
            normalizedBoardId,
            track: false,
            cancellationToken);
        if (existingItem is not null)
        {
            return new AddWatchlistItemResult(AddWatchlistItemStatus.AlreadyExists, ToDto(existingItem));
        }

        var symbolDetail = await _marketDataProvider.GetSymbolDetailAsync(
            normalizedSymbol,
            normalizedBoardId,
            cancellationToken);
        if (symbolDetail is null)
        {
            return new AddWatchlistItemResult(AddWatchlistItemStatus.SymbolNotFound, null);
        }

        var item = new WatchlistItem(
            groupId,
            normalizedSymbol,
            normalizedBoardId,
            _timeProvider.GetUtcNow());
        _dbContext.WatchlistItems.Add(item);

        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            var racedItem = await FindItemAsync(
                groupId,
                normalizedSymbol,
                normalizedBoardId,
                track: false,
                cancellationToken);
            if (racedItem is not null)
            {
                _dbContext.Entry(item).State = EntityState.Detached;
                return new AddWatchlistItemResult(AddWatchlistItemStatus.AlreadyExists, ToDto(racedItem));
            }

            throw;
        }

        return new AddWatchlistItemResult(AddWatchlistItemStatus.Created, ToDto(item));
    }

    public async Task<bool> RemoveItemAsync(
        Guid userId,
        Guid groupId,
        string symbol,
        string boardId,
        CancellationToken cancellationToken)
    {
        if (userId == Guid.Empty ||
            !TryNormalize(symbol, boardId, out var normalizedSymbol, out var normalizedBoardId))
        {
            return false;
        }

        var group = await FindGroupByIdAsync(
            userId,
            groupId,
            track: false,
            cancellationToken);
        if (group is null)
        {
            return false;
        }

        var item = await FindItemAsync(
            groupId,
            normalizedSymbol,
            normalizedBoardId,
            track: true,
            cancellationToken);
        if (item is null)
        {
            return false;
        }

        _dbContext.WatchlistItems.Remove(item);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    private async Task<WatchlistGroup?> FindGroupByIdAsync(
        Guid userId,
        Guid groupId,
        bool track,
        CancellationToken cancellationToken)
    {
        var query = _dbContext.WatchlistGroups
            .Where(group => group.UserId == userId && group.Id == groupId);

        if (!track)
        {
            query = query.AsNoTracking();
        }

        return await query.SingleOrDefaultAsync(cancellationToken);
    }

    private async Task<WatchlistGroup?> FindGroupByNameAsync(
        Guid userId,
        string name,
        bool track,
        CancellationToken cancellationToken)
    {
        var query = _dbContext.WatchlistGroups
            .Include(group => group.Items)
            .Where(group => group.UserId == userId && group.Name == name);

        if (!track)
        {
            query = query.AsNoTracking();
        }

        return await query.SingleOrDefaultAsync(cancellationToken);
    }

    private async Task<WatchlistItem?> FindItemAsync(
        Guid groupId,
        string symbol,
        string boardId,
        bool track,
        CancellationToken cancellationToken)
    {
        var query = _dbContext.WatchlistItems
            .Where(item =>
                item.GroupId == groupId &&
                item.Symbol == symbol &&
                item.BoardId == boardId);

        if (!track)
        {
            query = query.AsNoTracking();
        }

        return await query.SingleOrDefaultAsync(cancellationToken);
    }

    private static bool TryNormalize(
        string symbol,
        string boardId,
        out string normalizedSymbol,
        out string normalizedBoardId)
    {
        try
        {
            normalizedSymbol = MarketIdentity.NormalizeSymbol(symbol);
            normalizedBoardId = MarketIdentity.NormalizeBoardId(boardId);
            return true;
        }
        catch (ArgumentException)
        {
            normalizedSymbol = string.Empty;
            normalizedBoardId = string.Empty;
            return false;
        }
    }

    private static bool TryNormalizeName(string name, out string normalizedName)
    {
        try
        {
            normalizedName = WatchlistGroup.NormalizeName(name);
            return true;
        }
        catch (ArgumentException)
        {
            normalizedName = string.Empty;
            return false;
        }
    }

    private static WatchlistGroupDto ToDto(WatchlistGroup group)
    {
        return new WatchlistGroupDto(
            group.Id,
            group.Name,
            group.CreatedAt,
            group.UpdatedAt,
            group.Items
                .OrderBy(item => item.Symbol)
                .ThenBy(item => item.BoardId)
                .Select(ToDto)
                .ToArray());
    }

    private static WatchlistItemDto ToDto(WatchlistItem item)
    {
        return new WatchlistItemDto(
            item.Id,
            item.GroupId,
            item.Symbol,
            item.BoardId,
            item.CreatedAt);
    }
}
