using InvestView.Application.Abstractions.Trading;

namespace InvestView.Infrastructure.Trading;

public sealed class WeekdayTradingCalendar : ITradingCalendar
{
    private static readonly TimeSpan VietnamMarketOffset = TimeSpan.FromHours(7);

    public bool IsTradingDay(DateOnly date, string boardId)
    {
        _ = boardId;
        return date.DayOfWeek is not DayOfWeek.Saturday and not DayOfWeek.Sunday;
    }

    public DateOnly GetTradeDate(DateTimeOffset timestamp, string boardId)
    {
        _ = boardId;
        return DateOnly.FromDateTime(timestamp.ToOffset(VietnamMarketOffset).Date);
    }

    public DateOnly AddTradingDays(DateOnly tradeDate, int tradingDays, string boardId)
    {
        if (tradingDays < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(tradingDays), "Trading days cannot be negative.");
        }

        var date = tradeDate;
        var remaining = tradingDays;
        while (remaining > 0)
        {
            date = date.AddDays(1);
            if (IsTradingDay(date, boardId))
            {
                remaining--;
            }
        }

        return date;
    }
}

