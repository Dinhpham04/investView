using InvestView.Application.Abstractions.MarketData;
using InvestView.Application.Abstractions.Portfolio;
using InvestView.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace InvestView.Infrastructure.Trading;

public sealed class PortfolioService : IPortfolioService
{
    private readonly InvestViewDbContext _dbContext;
    private readonly IMarketDataProvider _marketDataProvider;
    private readonly TimeProvider _timeProvider;

    public PortfolioService(
        InvestViewDbContext dbContext,
        IMarketDataProvider marketDataProvider,
        TimeProvider timeProvider)
    {
        _dbContext = dbContext;
        _marketDataProvider = marketDataProvider;
        _timeProvider = timeProvider;
    }

    public async Task<PortfolioSnapshotDto?> GetSnapshotAsync(
        Guid userId,
        CancellationToken cancellationToken)
    {
        if (userId == Guid.Empty ||
            !await _dbContext.Users.AnyAsync(user => user.Id == userId, cancellationToken))
        {
            return null;
        }

        var cashAccounts = await _dbContext.CashAccounts
            .AsNoTracking()
            .Where(account => account.UserId == userId)
            .OrderBy(account => account.Currency)
            .Select(account => new CashAccountDto(
                account.Currency,
                account.Balance,
                account.AvailableBalance,
                account.UpdatedAt))
            .ToArrayAsync(cancellationToken);

        var holdings = await _dbContext.Holdings
            .AsNoTracking()
            .Where(holding => holding.UserId == userId && holding.Quantity > 0)
            .OrderBy(holding => holding.Symbol)
            .ThenBy(holding => holding.BoardId)
            .ToArrayAsync(cancellationToken);

        var positions = new List<HoldingPositionDto>(holdings.Length);
        foreach (var holding in holdings)
        {
            var detail = await _marketDataProvider.GetSymbolDetailAsync(
                holding.Symbol,
                holding.BoardId,
                cancellationToken);
            var lastPrice = detail?.LastPrice > 0m
                ? detail.LastPrice
                : detail?.ReferencePrice ?? 0m;
            var marketValue = holding.Quantity * lastPrice;
            var costValue = holding.Quantity * holding.AverageCost;

            positions.Add(new HoldingPositionDto(
                holding.Symbol,
                holding.BoardId,
                holding.Quantity,
                holding.AvailableQuantity,
                holding.AverageCost,
                lastPrice,
                marketValue,
                costValue,
                marketValue - costValue,
                holding.UpdatedAt));
        }

        var totalCash = cashAccounts.Sum(account => account.Balance);
        var totalAvailableCash = cashAccounts.Sum(account => account.AvailableBalance);
        var totalMarketValue = positions.Sum(position => position.MarketValue);
        var totalUnrealizedPnL = positions.Sum(position => position.UnrealizedPnL);

        return new PortfolioSnapshotDto(
            cashAccounts,
            positions,
            totalCash,
            totalAvailableCash,
            totalMarketValue,
            totalCash + totalMarketValue,
            totalUnrealizedPnL,
            _timeProvider.GetUtcNow());
    }
}
