using InvestView.Application.Dtos.MarketData;
using InvestView.Infrastructure.MarketData;

namespace InvestView.Api.Tests.MarketData;

public sealed class MarketSessionResolverTests
{
    [Fact]
    public void Resolve_WhenHoseMainBoardIsInAtoWindow_ReturnsAto()
    {
        var session = new MarketSessionUpdateDto(
            MarketId: "HOSE",
            BoardId: "G1",
            ProductGroupId: "STO",
            EventId: string.Empty,
            TradingSessionId: string.Empty,
            UpdatedAt: Utc(2026, 7, 13, 2, 5));

        var resolved = MarketSessionResolver.Resolve(session);

        Assert.Equal(MarketSessionPhases.Ato, resolved.Phase);
        Assert.Equal("ATO", resolved.Label);
        Assert.True(resolved.IsOpen);
        Assert.True(resolved.IsAuction);
        Assert.False(resolved.IsContinuous);
        Assert.Equal(MarketSessionSources.ScheduleFallback, resolved.Source);
    }

    [Fact]
    public void Resolve_WhenDnseSessionCodeIsContinuous_ReturnsRealtimeContinuous()
    {
        var session = new MarketSessionUpdateDto(
            MarketId: "DVX",
            BoardId: "G1",
            ProductGroupId: "STO",
            EventId: "AB2",
            TradingSessionId: "40",
            UpdatedAt: Utc(2026, 7, 13, 2, 15));

        var resolved = MarketSessionResolver.Resolve(session);

        Assert.Equal(MarketSessionPhases.Continuous, resolved.Phase);
        Assert.Equal("Liên tục", resolved.Label);
        Assert.True(resolved.IsOpen);
        Assert.True(resolved.IsContinuous);
        Assert.Equal(MarketSessionSources.Realtime, resolved.Source);
    }

    [Fact]
    public void Resolve_WhenCachedContinuousSessionIsEvaluatedDuringLunchBreak_ReturnsScheduledLunchBreak()
    {
        var session = new MarketSessionUpdateDto(
            MarketId: "HOSE",
            BoardId: "G1",
            ProductGroupId: "STO",
            EventId: "AB2",
            TradingSessionId: "40",
            UpdatedAt: Utc(2026, 7, 13, 2, 20));

        var resolved = MarketSessionResolver.Resolve(session, Utc(2026, 7, 13, 5, 5));

        Assert.Equal(MarketSessionPhases.LunchBreak, resolved.Phase);
        Assert.Equal(MarketSessionSources.ScheduleFallback, resolved.Source);
        Assert.False(resolved.IsOpen);
        Assert.False(resolved.IsContinuous);
    }

    [Fact]
    public void Resolve_WhenMainBoardIsInLunchBreak_ReturnsLunchBreak()
    {
        var session = new MarketSessionUpdateDto(
            MarketId: "HOSE",
            BoardId: "G1",
            ProductGroupId: "STO",
            EventId: string.Empty,
            TradingSessionId: string.Empty,
            UpdatedAt: Utc(2026, 7, 13, 5, 0));

        var resolved = MarketSessionResolver.Resolve(session);

        Assert.Equal(MarketSessionPhases.LunchBreak, resolved.Phase);
        Assert.Equal("Nghỉ trưa", resolved.Label);
        Assert.False(resolved.IsOpen);
        Assert.Equal(MarketSessionSources.ScheduleFallback, resolved.Source);
    }

    [Fact]
    public void Resolve_WhenHnxPloBoardIsInAfterHoursWindow_ReturnsPloAfterHours()
    {
        var session = new MarketSessionUpdateDto(
            MarketId: "HNX",
            BoardId: "G3",
            ProductGroupId: "STX",
            EventId: string.Empty,
            TradingSessionId: string.Empty,
            UpdatedAt: Utc(2026, 7, 13, 7, 50));

        var resolved = MarketSessionResolver.Resolve(session);

        Assert.Equal(MarketSessionPhases.Plo, resolved.Phase);
        Assert.Equal("PLO sau giờ", resolved.Label);
        Assert.True(resolved.IsOpen);
        Assert.True(resolved.IsAfterHours);
        Assert.Equal(MarketSessionSources.ScheduleFallback, resolved.Source);
    }

    [Fact]
    public void Resolve_WhenWeekend_ReturnsClosed()
    {
        var session = new MarketSessionUpdateDto(
            MarketId: "HOSE",
            BoardId: "G1",
            ProductGroupId: "STO",
            EventId: string.Empty,
            TradingSessionId: string.Empty,
            UpdatedAt: Utc(2026, 7, 18, 3, 0));

        var resolved = MarketSessionResolver.Resolve(session);

        Assert.Equal(MarketSessionPhases.Closed, resolved.Phase);
        Assert.Equal("Đã đóng cửa", resolved.Label);
        Assert.False(resolved.IsOpen);
        Assert.Equal(MarketSessionSources.ScheduleFallback, resolved.Source);
    }

    private static DateTimeOffset Utc(int year, int month, int day, int hour, int minute)
    {
        return new DateTimeOffset(year, month, day, hour, minute, 0, TimeSpan.Zero);
    }
}
