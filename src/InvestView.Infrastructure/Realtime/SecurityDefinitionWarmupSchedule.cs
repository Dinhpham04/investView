using Microsoft.Extensions.Options;

namespace InvestView.Infrastructure.Realtime;

public sealed class SecurityDefinitionWarmupSchedule
{
    private static readonly TimeSpan MinimumRecheckInterval = TimeSpan.FromSeconds(5);
    private readonly IOptions<SecurityDefinitionWarmupOptions> _options;

    public SecurityDefinitionWarmupSchedule(IOptions<SecurityDefinitionWarmupOptions> options)
    {
        _options = options;
    }

    public SecurityDefinitionWarmupDecision Evaluate(DateTimeOffset utcNow, DateOnly? lastRunLocalDate)
    {
        var schedule = _options.Value.Schedule;
        var recheckAfter = GetRecheckInterval(schedule);

        var timeZone = ResolveTimeZone(schedule.TimeZoneId);
        var localNow = TimeZoneInfo.ConvertTime(utcNow, timeZone);
        var localDate = DateOnly.FromDateTime(localNow.DateTime);

        if (lastRunLocalDate == localDate)
        {
            return new SecurityDefinitionWarmupDecision(
                false,
                recheckAfter,
                localDate,
                $"Security definition warmup already ran for {localDate:yyyy-MM-dd}.");
        }

        if (!schedule.Enabled)
        {
            return new SecurityDefinitionWarmupDecision(
                true,
                recheckAfter,
                localDate,
                "Security definition warmup schedule gate is disabled.");
        }

        if (!IsActiveDay(localNow.DayOfWeek, schedule.ActiveDays))
        {
            return new SecurityDefinitionWarmupDecision(
                false,
                recheckAfter,
                localDate,
                $"Security definition warmup is outside active warmup days in {timeZone.Id}.");
        }

        if (!IsWithinWindow(localNow.TimeOfDay, schedule.StartLocalTime, schedule.EndLocalTime))
        {
            return new SecurityDefinitionWarmupDecision(
                false,
                recheckAfter,
                localDate,
                $"Security definition warmup is outside window {schedule.StartLocalTime:hh\\:mm}-{schedule.EndLocalTime:hh\\:mm} {timeZone.Id}.");
        }

        return new SecurityDefinitionWarmupDecision(
            true,
            recheckAfter,
            localDate,
            $"Security definition warmup is inside window {schedule.StartLocalTime:hh\\:mm}-{schedule.EndLocalTime:hh\\:mm} {timeZone.Id}.");
    }

    private static TimeSpan GetRecheckInterval(SecurityDefinitionWarmupScheduleOptions schedule)
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

public sealed record SecurityDefinitionWarmupDecision(
    bool ShouldRun,
    TimeSpan RecheckAfter,
    DateOnly LocalDate,
    string Message);
