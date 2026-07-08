using InvestView.Application.Abstractions.Realtime;
using Microsoft.Extensions.Options;

namespace InvestView.Infrastructure.Realtime;

public sealed class MarketQuoteStreamSchedule
{
    private static readonly TimeSpan MinimumRecheckInterval = TimeSpan.FromSeconds(5);
    private readonly IOptions<MarketQuoteStreamOptions> _options;

    public MarketQuoteStreamSchedule(IOptions<MarketQuoteStreamOptions> options)
    {
        _options = options;
    }

    public MarketQuoteStreamConnectionDecision Evaluate(
        MarketQuoteSubscriptionSnapshot snapshot,
        DateTimeOffset utcNow)
    {
        var schedule = _options.Value.Schedule;
        var recheckAfter = GetRecheckInterval(schedule);

        if (schedule.RequireActiveSubscriptions && !HasActiveSymbols(snapshot))
        {
            return new MarketQuoteStreamConnectionDecision(
                ShouldConnect: false,
                recheckAfter,
                "DNSE websocket waiting for active market-board subscriptions.");
        }

        if (!schedule.Enabled)
        {
            return new MarketQuoteStreamConnectionDecision(
                ShouldConnect: true,
                recheckAfter,
                "DNSE websocket schedule gate is disabled.");
        }

        var timeZone = ResolveTimeZone(schedule.TimeZoneId);
        var localNow = TimeZoneInfo.ConvertTime(utcNow, timeZone);
        if (!IsActiveDay(localNow.DayOfWeek, schedule.ActiveDays))
        {
            return new MarketQuoteStreamConnectionDecision(
                ShouldConnect: false,
                recheckAfter,
                $"DNSE websocket is outside active trading days in {timeZone.Id}.");
        }

        if (!IsWithinWindow(localNow.TimeOfDay, schedule.ConnectStartLocalTime, schedule.ConnectEndLocalTime))
        {
            return new MarketQuoteStreamConnectionDecision(
                ShouldConnect: false,
                recheckAfter,
                $"DNSE websocket is outside streaming window {schedule.ConnectStartLocalTime:hh\\:mm}-{schedule.ConnectEndLocalTime:hh\\:mm} {timeZone.Id}.");
        }

        return new MarketQuoteStreamConnectionDecision(
            ShouldConnect: true,
            recheckAfter,
            $"DNSE websocket is inside streaming window {schedule.ConnectStartLocalTime:hh\\:mm}-{schedule.ConnectEndLocalTime:hh\\:mm} {timeZone.Id}.");
    }

    private static bool HasActiveSymbols(MarketQuoteSubscriptionSnapshot snapshot)
    {
        return snapshot.Boards.Any(board => board.Symbols.Count > 0);
    }

    private static TimeSpan GetRecheckInterval(MarketQuoteStreamScheduleOptions schedule)
    {
        var configured = TimeSpan.FromSeconds(Math.Max(1, schedule.RecheckIntervalSeconds));
        return configured < MinimumRecheckInterval ? MinimumRecheckInterval : configured;
    }

    private static TimeZoneInfo ResolveTimeZone(string configuredTimeZoneId)
    {
        foreach (var timeZoneId in CandidateTimeZoneIds(configuredTimeZoneId))
        {
            try
            {
                return TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
            }
            catch (TimeZoneNotFoundException)
            {
            }
            catch (InvalidTimeZoneException)
            {
            }
        }

        return TimeZoneInfo.Utc;
    }

    private static IEnumerable<string> CandidateTimeZoneIds(string configuredTimeZoneId)
    {
        if (!string.IsNullOrWhiteSpace(configuredTimeZoneId))
        {
            yield return configuredTimeZoneId;
        }

        yield return "Asia/Ho_Chi_Minh";
        yield return "SE Asia Standard Time";
    }

    private static bool IsActiveDay(DayOfWeek dayOfWeek, IReadOnlyCollection<string> activeDays)
    {
        if (activeDays.Count == 0)
        {
            return true;
        }

        return activeDays.Any(day =>
            Enum.TryParse<DayOfWeek>(day, ignoreCase: true, out var configuredDay)
            && configuredDay == dayOfWeek);
    }

    private static bool IsWithinWindow(TimeSpan localTime, TimeSpan start, TimeSpan end)
    {
        return start <= end
            ? localTime >= start && localTime <= end
            : localTime >= start || localTime <= end;
    }
}

public sealed record MarketQuoteStreamConnectionDecision(
    bool ShouldConnect,
    TimeSpan RecheckAfter,
    string Message);
