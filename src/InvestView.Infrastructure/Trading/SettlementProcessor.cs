using InvestView.Application.Abstractions.Trading;
using InvestView.Domain.Trading;
using InvestView.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace InvestView.Infrastructure.Trading;

public sealed class SettlementProcessor : ISettlementProcessor
{
    private readonly InvestViewDbContext _dbContext;
    private readonly ITradingCalendar _tradingCalendar;
    private readonly TimeProvider _timeProvider;

    public SettlementProcessor(
        InvestViewDbContext dbContext,
        ITradingCalendar tradingCalendar,
        TimeProvider timeProvider)
    {
        _dbContext = dbContext;
        _tradingCalendar = tradingCalendar;
        _timeProvider = timeProvider;
    }

    public async Task<SettlementRunDto> SettleDueLotsAsync(
        Guid? triggeredByUserId,
        CancellationToken cancellationToken)
    {
        var now = _timeProvider.GetUtcNow();
        var today = _tradingCalendar.GetTradeDate(now, "G1");
        var query = _dbContext.HoldingSettlementLots
            .Where(lot =>
                lot.Status == HoldingSettlementLotStatus.Pending &&
                lot.AvailableFromDate <= today);

        if (triggeredByUserId is { } userId)
        {
            query = query.Where(lot => lot.UserId == userId);
        }

        var dueLots = await query
            .OrderBy(lot => lot.AvailableFromDate)
            .ThenBy(lot => lot.CreatedAt)
            .ToArrayAsync(cancellationToken);

        if (dueLots.Length == 0)
        {
            return new SettlementRunDto(
                Guid.Empty,
                now,
                now,
                DueLotCount: 0,
                SettledLotCount: 0,
                FailedLotCount: 0);
        }

        var run = new SettlementRun(triggeredByUserId, now);
        _dbContext.SettlementRuns.Add(run);

        var settledCount = 0;
        var failedCount = 0;

        foreach (var lot in dueLots)
        {
            try
            {
                var holding = await _dbContext.Holdings.SingleAsync(
                    holding =>
                        holding.UserId == lot.UserId &&
                        holding.BoardId == lot.BoardId &&
                        holding.Symbol == lot.Symbol,
                    cancellationToken);

                holding.SettleReceivedQuantity(lot.RemainingQuantity, now);
                lot.MarkSettled(now);
                settledCount += 1;
            }
            catch (InvalidOperationException)
            {
                lot.MarkFailed(now);
                failedCount += 1;
            }
        }

        run.Complete(dueLots.Length, settledCount, failedCount, now);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return new SettlementRunDto(
            run.Id,
            run.StartedAt,
            run.CompletedAt,
            run.DueLotCount,
            run.SettledLotCount,
            run.FailedLotCount);
    }
}
