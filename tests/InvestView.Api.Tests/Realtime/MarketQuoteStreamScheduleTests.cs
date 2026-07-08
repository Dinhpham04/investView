using InvestView.Application.Abstractions.Realtime;
using InvestView.Infrastructure.Realtime;
using Microsoft.Extensions.Options;

namespace InvestView.Api.Tests.Realtime;

public sealed class MarketQuoteStreamScheduleTests
{
    [Fact]
    public void Evaluate_WhenNoActiveSubscriptions_ReturnsDoNotConnect()
    {
        var schedule = CreateSchedule();

        var decision = schedule.Evaluate(
            MarketQuoteSubscriptionSnapshot.Empty,
            new DateTimeOffset(2026, 7, 8, 3, 0, 0, TimeSpan.Zero));

        Assert.False(decision.ShouldConnect);
        Assert.Contains("waiting for active", decision.Message);
    }

    [Fact]
    public void Evaluate_WhenInsideStreamingWindowAndHasActiveSymbols_ReturnsConnect()
    {
        var schedule = CreateSchedule();
        var snapshot = CreateSnapshot("G1", "HPG");

        var decision = schedule.Evaluate(
            snapshot,
            new DateTimeOffset(2026, 7, 8, 3, 0, 0, TimeSpan.Zero));

        Assert.True(decision.ShouldConnect);
        Assert.Contains("inside streaming window", decision.Message);
    }

    [Fact]
    public void Evaluate_WhenOutsideStreamingWindow_ReturnsDoNotConnect()
    {
        var schedule = CreateSchedule();
        var snapshot = CreateSnapshot("G1", "HPG");

        var decision = schedule.Evaluate(
            snapshot,
            new DateTimeOffset(2026, 7, 8, 10, 0, 0, TimeSpan.Zero));

        Assert.False(decision.ShouldConnect);
        Assert.Contains("outside streaming window", decision.Message);
    }

    [Fact]
    public void Evaluate_WhenWeekend_ReturnsDoNotConnect()
    {
        var schedule = CreateSchedule();
        var snapshot = CreateSnapshot("G1", "HPG");

        var decision = schedule.Evaluate(
            snapshot,
            new DateTimeOffset(2026, 7, 11, 3, 0, 0, TimeSpan.Zero));

        Assert.False(decision.ShouldConnect);
        Assert.Contains("outside active trading days", decision.Message);
    }

    [Fact]
    public void Evaluate_WhenScheduleGateIsDisabled_StillRequiresActiveSymbols()
    {
        var schedule = CreateSchedule(options => options.Schedule.Enabled = false);

        var withoutSymbols = schedule.Evaluate(
            MarketQuoteSubscriptionSnapshot.Empty,
            new DateTimeOffset(2026, 7, 11, 10, 0, 0, TimeSpan.Zero));
        var withSymbols = schedule.Evaluate(
            CreateSnapshot("G1", "HPG"),
            new DateTimeOffset(2026, 7, 11, 10, 0, 0, TimeSpan.Zero));

        Assert.False(withoutSymbols.ShouldConnect);
        Assert.True(withSymbols.ShouldConnect);
    }

    private static MarketQuoteStreamSchedule CreateSchedule(Action<MarketQuoteStreamOptions>? configure = null)
    {
        var options = new MarketQuoteStreamOptions
        {
            Schedule =
            {
                TimeZoneId = "Asia/Ho_Chi_Minh",
                ConnectStartLocalTime = new TimeSpan(7, 50, 0),
                ConnectEndLocalTime = new TimeSpan(15, 30, 0),
                RecheckIntervalSeconds = 30
            }
        };
        configure?.Invoke(options);

        return new MarketQuoteStreamSchedule(Options.Create(options));
    }

    private static MarketQuoteSubscriptionSnapshot CreateSnapshot(string boardId, params string[] symbols)
    {
        return new MarketQuoteSubscriptionSnapshot(
            [new MarketQuoteBoardSubscription(boardId, symbols)],
            Version: 1);
    }
}
