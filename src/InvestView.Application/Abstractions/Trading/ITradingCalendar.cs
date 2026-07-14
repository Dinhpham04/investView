namespace InvestView.Application.Abstractions.Trading;

public interface ITradingCalendar
{
    bool IsTradingDay(DateOnly date, string boardId);

    DateOnly GetTradeDate(DateTimeOffset timestamp, string boardId);

    DateOnly AddTradingDays(DateOnly tradeDate, int tradingDays, string boardId);
}

