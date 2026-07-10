using InvestView.Infrastructure.Realtime;
using Microsoft.Extensions.Options;

namespace InvestView.Api.Tests.Realtime;

public sealed class SecurityDefinitionWarmupScheduleTests
{
    [Fact]
    public void Evaluate_WhenInsideWindowAndNotRunToday_ShouldRun()
    {
        var schedule = new SecurityDefinitionWarmupSchedule(Options.Create(new SecurityDefinitionWarmupOptions()));

        var decision = schedule.Evaluate(
            new DateTimeOffset(2026, 7, 10, 0, 56, 0, TimeSpan.Zero),
            lastRunLocalDate: null);

        Assert.True(decision.ShouldRun);
        Assert.Equal(new DateOnly(2026, 7, 10), decision.LocalDate);
    }

    [Fact]
    public void Evaluate_WhenAlreadyRunToday_ShouldWait()
    {
        var schedule = new SecurityDefinitionWarmupSchedule(Options.Create(new SecurityDefinitionWarmupOptions()));

        var decision = schedule.Evaluate(
            new DateTimeOffset(2026, 7, 10, 1, 0, 0, TimeSpan.Zero),
            new DateOnly(2026, 7, 10));

        Assert.False(decision.ShouldRun);
        Assert.Contains("already ran", decision.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Evaluate_WhenOutsideActiveDays_ShouldWait()
    {
        var schedule = new SecurityDefinitionWarmupSchedule(Options.Create(new SecurityDefinitionWarmupOptions()));

        var decision = schedule.Evaluate(
            new DateTimeOffset(2026, 7, 11, 1, 0, 0, TimeSpan.Zero),
            lastRunLocalDate: null);

        Assert.False(decision.ShouldRun);
        Assert.Contains("outside active warmup days", decision.Message, StringComparison.OrdinalIgnoreCase);
    }
}
