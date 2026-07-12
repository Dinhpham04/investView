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

    public async Task<IReadOnlyList<WatchlistItemDto>> ListAsync(
        Guid userId,
        CancellationToken cancellationToken)
    {
        if (userId == Guid.Empty)
        {
            return [];
        }

        return await _dbContext.WatchlistItems
            .AsNoTracking()
            .Where(item => item.UserId == userId)
            .OrderBy(item => item.Symbol)
            .ThenBy(item => item.BoardId)
            .Select(item => new WatchlistItemDto(
                item.Id,
                item.Symbol,
                item.BoardId,
                item.CreatedAt))
            .ToArrayAsync(cancellationToken);
    }

    public async Task<AddWatchlistItemResult> AddAsync(
        Guid userId,
        string symbol,
        string boardId,
        CancellationToken cancellationToken)
    {
        if (!TryNormalize(symbol, boardId, out var normalizedSymbol, out var normalizedBoardId))
        {
            return new AddWatchlistItemResult(AddWatchlistItemStatus.InvalidInput, null);
        }

        if (userId == Guid.Empty ||
            !await _dbContext.Users.AnyAsync(user => user.Id == userId, cancellationToken))
        {
            return new AddWatchlistItemResult(AddWatchlistItemStatus.UserNotFound, null);
        }

        var existingItem = await FindItemAsync(
            userId,
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
            userId,
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
                userId,
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

    public async Task<bool> RemoveAsync(
        Guid userId,
        string symbol,
        string boardId,
        CancellationToken cancellationToken)
    {
        if (userId == Guid.Empty ||
            !TryNormalize(symbol, boardId, out var normalizedSymbol, out var normalizedBoardId))
        {
            return false;
        }

        var item = await FindItemAsync(
            userId,
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

    private async Task<WatchlistItem?> FindItemAsync(
        Guid userId,
        string symbol,
        string boardId,
        bool track,
        CancellationToken cancellationToken)
    {
        var query = _dbContext.WatchlistItems
            .Where(item =>
                item.UserId == userId &&
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

    private static WatchlistItemDto ToDto(WatchlistItem item)
    {
        return new WatchlistItemDto(
            item.Id,
            item.Symbol,
            item.BoardId,
            item.CreatedAt);
    }
}
