using InvestView.Application.Abstractions.MarketData;
using InvestView.Application.Abstractions.Portfolio;
using InvestView.Application.Abstractions.Trading;
using InvestView.Domain.Trading;
using InvestView.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace InvestView.Infrastructure.Trading;

public sealed class PortfolioService : IPortfolioService
{
    private readonly InvestViewDbContext _dbContext;
    private readonly IMarketDataProvider _marketDataProvider;
    private readonly ISettlementProcessor _settlementProcessor;
    private readonly ITradingCalendar _tradingCalendar;
    private readonly TimeProvider _timeProvider;

    public PortfolioService(
        InvestViewDbContext dbContext,
        IMarketDataProvider marketDataProvider,
        ISettlementProcessor settlementProcessor,
        ITradingCalendar tradingCalendar,
        TimeProvider timeProvider)
    {
        _dbContext = dbContext;
        _marketDataProvider = marketDataProvider;
        _settlementProcessor = settlementProcessor;
        _tradingCalendar = tradingCalendar;
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

        await _settlementProcessor.SettleDueLotsAsync(userId, cancellationToken);

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
                holding.PendingReceiveQuantity,
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

    public async Task<PortfolioHoldingsSnapshotDto?> GetHoldingsAsync(
        Guid userId,
        CancellationToken cancellationToken)
    {
        if (userId == Guid.Empty ||
            !await _dbContext.Users.AnyAsync(user => user.Id == userId, cancellationToken))
        {
            return null;
        }

        await _settlementProcessor.SettleDueLotsAsync(userId, cancellationToken);

        var now = _timeProvider.GetUtcNow();
        var today = _tradingCalendar.GetTradeDate(now, "G1");
        var holdings = await _dbContext.Holdings
            .AsNoTracking()
            .Where(holding => holding.UserId == userId && holding.Quantity > 0)
            .OrderBy(holding => holding.Symbol)
            .ThenBy(holding => holding.BoardId)
            .ToArrayAsync(cancellationToken);

        var pendingLots = await _dbContext.HoldingSettlementLots
            .AsNoTracking()
            .Where(lot => lot.UserId == userId && lot.Status == HoldingSettlementLotStatus.Pending)
            .ToArrayAsync(cancellationToken);
        var pendingLotsByHolding = pendingLots
            .GroupBy(lot => GetHoldingKey(lot.BoardId, lot.Symbol))
            .ToDictionary(group => group.Key, group => group.ToArray());

        var positions = new List<PortfolioHoldingDto>(holdings.Length);
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
            var unrealizedPnL = marketValue - costValue;
            var holdingPendingLots = pendingLotsByHolding.GetValueOrDefault(GetHoldingKey(holding.BoardId, holding.Symbol)) ?? [];
            var pendingT0 = 0L;
            var pendingT1 = 0L;
            var pendingT2 = 0L;

            foreach (var lot in holdingPendingLots)
            {
                switch (GetSettlementAgeBucket(lot, today))
                {
                    case 0:
                        pendingT0 += lot.RemainingQuantity;
                        break;
                    case 1:
                        pendingT1 += lot.RemainingQuantity;
                        break;
                    default:
                        pendingT2 += lot.RemainingQuantity;
                        break;
                }
            }

            positions.Add(new PortfolioHoldingDto(
                holding.Symbol,
                holding.BoardId,
                holding.Quantity,
                holding.AvailableQuantity,
                holding.PendingReceiveQuantity,
                pendingT0,
                pendingT1,
                pendingT2,
                holdingPendingLots.Length == 0 ? null : holdingPendingLots.Min(lot => lot.AvailableFromDate),
                holding.AverageCost,
                lastPrice,
                costValue,
                marketValue,
                unrealizedPnL,
                costValue <= 0m ? 0m : unrealizedPnL / costValue * 100m,
                holding.UpdatedAt));
        }

        return new PortfolioHoldingsSnapshotDto(
            positions,
            positions.Sum(position => position.Quantity),
            positions.Sum(position => position.AvailableQuantity),
            positions.Sum(position => position.PendingReceiveQuantity),
            positions.Sum(position => position.CostValue),
            positions.Sum(position => position.MarketValue),
            positions.Sum(position => position.UnrealizedPnL),
            now);
    }

    private int GetSettlementAgeBucket(HoldingSettlementLot lot, DateOnly today)
    {
        var age = 0;
        var date = lot.TradeDate;
        while (date < today && age < 2)
        {
            date = _tradingCalendar.AddTradingDays(date, 1, lot.BoardId);
            age += 1;
        }

        return age;
    }

    private static string GetHoldingKey(string boardId, string symbol)
    {
        return $"{boardId}:{symbol}";
    }
}
