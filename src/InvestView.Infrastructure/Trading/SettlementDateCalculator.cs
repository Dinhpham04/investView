using InvestView.Application.Abstractions.Trading;

namespace InvestView.Infrastructure.Trading;

public sealed class SettlementDateCalculator : ISettlementDateCalculator
{
    private const int StockSettlementTradingDays = 2;

    private readonly ITradingCalendar _tradingCalendar;

    public SettlementDateCalculator(ITradingCalendar tradingCalendar)
    {
        _tradingCalendar = tradingCalendar;
    }

    public SettlementDates CalculateStockSettlement(string boardId, DateTimeOffset executionTime)
    {
        var tradeDate = _tradingCalendar.GetTradeDate(executionTime, boardId);
        var settlementDate = _tradingCalendar.AddTradingDays(tradeDate, StockSettlementTradingDays, boardId);
        return new SettlementDates(
            TradeDate: tradeDate,
            SettlementDate: settlementDate,
            AvailableFromDate: settlementDate);
    }
}

