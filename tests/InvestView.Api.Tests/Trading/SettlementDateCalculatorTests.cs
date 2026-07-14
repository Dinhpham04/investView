using InvestView.Infrastructure.Trading;

namespace InvestView.Api.Tests.Trading;

public sealed class SettlementDateCalculatorTests
{
    [Fact]
    public void CalculateStockSettlement_WhenTradeIsMonday_ShouldSettleOnWednesday()
    {
        var calculator = new SettlementDateCalculator(new WeekdayTradingCalendar());

        var dates = calculator.CalculateStockSettlement(
            "G1",
            new DateTimeOffset(2026, 7, 13, 3, 0, 0, TimeSpan.Zero));

        Assert.Equal(new DateOnly(2026, 7, 13), dates.TradeDate);
        Assert.Equal(new DateOnly(2026, 7, 15), dates.SettlementDate);
        Assert.Equal(new DateOnly(2026, 7, 15), dates.AvailableFromDate);
    }

    [Fact]
    public void CalculateStockSettlement_WhenTradeIsFriday_ShouldSkipWeekend()
    {
        var calculator = new SettlementDateCalculator(new WeekdayTradingCalendar());

        var dates = calculator.CalculateStockSettlement(
            "G1",
            new DateTimeOffset(2026, 7, 17, 3, 0, 0, TimeSpan.Zero));

        Assert.Equal(new DateOnly(2026, 7, 17), dates.TradeDate);
        Assert.Equal(new DateOnly(2026, 7, 21), dates.SettlementDate);
        Assert.Equal(new DateOnly(2026, 7, 21), dates.AvailableFromDate);
    }
}

