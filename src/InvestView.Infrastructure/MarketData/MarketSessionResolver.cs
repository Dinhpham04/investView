using InvestView.Application.Dtos.MarketData;

namespace InvestView.Infrastructure.MarketData;

public static class MarketSessionResolver
{
    private static readonly TimeSpan RealtimeSessionFreshnessWindow = TimeSpan.FromMinutes(10);
    private static readonly TimeZoneInfo VietnamTimeZone = ResolveVietnamTimeZone();

    public static MarketSessionUpdateDto Resolve(MarketSessionUpdateDto session)
    {
        return Resolve(session, session.UpdatedAt);
    }

    public static MarketSessionUpdateDto Resolve(MarketSessionUpdateDto session, DateTimeOffset asOf)
    {
        var normalized = session with
        {
            MarketId = MarketStateMapper.Normalize(session.MarketId),
            BoardId = MarketStateMapper.NormalizeBoardId(session.BoardId),
            ProductGroupId = MarketStateMapper.Normalize(session.ProductGroupId),
            EventId = MarketStateMapper.Normalize(session.EventId),
            TradingSessionId = MarketStateMapper.Normalize(session.TradingSessionId)
        };

        var scheduledSession = normalized with { UpdatedAt = asOf };
        var scheduledPhase = ResolveScheduledPhase(scheduledSession);
        var realtimePhase = ResolveRealtimePhase(normalized);
        if (ShouldUseRealtimePhase(realtimePhase, scheduledPhase, normalized.UpdatedAt, asOf))
        {
            return ApplyPhase(normalized, realtimePhase!, MarketSessionSources.Realtime);
        }

        return ApplyPhase(scheduledSession, scheduledPhase, MarketSessionSources.ScheduleFallback);
    }

    private static MarketSessionPhase ResolveScheduledPhase(MarketSessionUpdateDto session)
    {
        var localTime = TimeZoneInfo.ConvertTime(session.UpdatedAt, VietnamTimeZone);
        if (localTime.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday)
        {
            return MarketSessionPhase.Closed;
        }

        var time = TimeOnly.FromDateTime(localTime.DateTime);
        var productGroup = session.ProductGroupId;
        var boardId = session.BoardId;
        var marketId = session.MarketId;

        if (boardId is "T1")
        {
            return InRange(time, "09:00", "14:45") ? MarketSessionPhase.PutThrough : MarketSessionPhase.Closed;
        }

        if (boardId is "T3")
        {
            return InRange(time, "14:45", "15:00") ? MarketSessionPhase.PutThrough : MarketSessionPhase.Closed;
        }

        if (boardId is "G3")
        {
            return InRange(time, "14:45", "15:00") ? MarketSessionPhase.Plo : ResolvePreOpenOrClosed(time);
        }

        if (IsUpcom(productGroup, marketId))
        {
            return ResolveUpcomSchedule(time);
        }

        if (IsHnx(productGroup, marketId))
        {
            return ResolveHnxSchedule(time);
        }

        return ResolveHoseSchedule(time);
    }

    private static MarketSessionPhase? ResolveRealtimePhase(MarketSessionUpdateDto session)
    {
        var eventId = session.EventId;
        var tradingSessionId = session.TradingSessionId;

        if (ContainsAny(eventId, "ATO", "OPEN_AUCTION") || tradingSessionId is "ATO" or "20")
        {
            return MarketSessionPhase.Ato;
        }

        if (ContainsAny(eventId, "ATC", "CLOSE_AUCTION") || tradingSessionId is "ATC" or "50")
        {
            return MarketSessionPhase.Atc;
        }

        if (ContainsAny(eventId, "PLO", "AFTER") || tradingSessionId is "PLO" or "60")
        {
            return MarketSessionPhase.Plo;
        }

        if (ContainsAny(eventId, "PUT", "PT", "NEGOTIATED", "DEAL") || session.BoardId is "T1" or "T3")
        {
            return MarketSessionPhase.PutThrough;
        }

        if (eventId is "AB2" || tradingSessionId is "40" or "CONTINUOUS" or "LO")
        {
            return MarketSessionPhase.Continuous;
        }

        if (ContainsAny(eventId, "BREAK", "LUNCH") || tradingSessionId is "LUNCH")
        {
            return MarketSessionPhase.LunchBreak;
        }

        if (ContainsAny(eventId, "CLOSE", "CLOSED", "END") || tradingSessionId is "CLOSED" or "99")
        {
            return MarketSessionPhase.Closed;
        }

        return null;
    }

    private static bool ShouldUseRealtimePhase(
        MarketSessionPhase? realtimePhase,
        MarketSessionPhase scheduledPhase,
        DateTimeOffset realtimeUpdatedAt,
        DateTimeOffset asOf)
    {
        if (realtimePhase is null)
        {
            return false;
        }

        if (realtimePhase.Code.Equals(scheduledPhase.Code, StringComparison.Ordinal))
        {
            return true;
        }

        if (realtimePhase.Code.Equals(MarketSessionPhases.Continuous, StringComparison.Ordinal))
        {
            return false;
        }

        return IsRealtimeSessionFresh(realtimeUpdatedAt, asOf);
    }

    private static bool IsRealtimeSessionFresh(DateTimeOffset updatedAt, DateTimeOffset asOf)
    {
        return updatedAt <= asOf && asOf - updatedAt <= RealtimeSessionFreshnessWindow;
    }

    private static MarketSessionPhase ResolveHoseSchedule(TimeOnly time)
    {
        if (time < T("09:00"))
        {
            return MarketSessionPhase.PreOpen;
        }

        if (InRange(time, "09:00", "09:15"))
        {
            return MarketSessionPhase.Ato;
        }

        if (InRange(time, "09:15", "11:30") || InRange(time, "13:00", "14:30"))
        {
            return MarketSessionPhase.Continuous;
        }

        if (InRange(time, "11:30", "13:00"))
        {
            return MarketSessionPhase.LunchBreak;
        }

        if (InRange(time, "14:30", "14:45"))
        {
            return MarketSessionPhase.Atc;
        }

        if (InRange(time, "14:45", "15:00"))
        {
            return MarketSessionPhase.PutThrough;
        }

        return MarketSessionPhase.Closed;
    }

    private static MarketSessionPhase ResolveHnxSchedule(TimeOnly time)
    {
        if (time < T("09:00"))
        {
            return MarketSessionPhase.PreOpen;
        }

        if (InRange(time, "09:00", "11:30") || InRange(time, "13:00", "14:30"))
        {
            return MarketSessionPhase.Continuous;
        }

        if (InRange(time, "11:30", "13:00"))
        {
            return MarketSessionPhase.LunchBreak;
        }

        if (InRange(time, "14:30", "14:45"))
        {
            return MarketSessionPhase.Atc;
        }

        if (InRange(time, "14:45", "15:00"))
        {
            return MarketSessionPhase.Plo;
        }

        return MarketSessionPhase.Closed;
    }

    private static MarketSessionPhase ResolveUpcomSchedule(TimeOnly time)
    {
        if (time < T("09:00"))
        {
            return MarketSessionPhase.PreOpen;
        }

        if (InRange(time, "09:00", "11:30") || InRange(time, "13:00", "15:00"))
        {
            return MarketSessionPhase.Continuous;
        }

        if (InRange(time, "11:30", "13:00"))
        {
            return MarketSessionPhase.LunchBreak;
        }

        return MarketSessionPhase.Closed;
    }

    private static MarketSessionPhase ResolvePreOpenOrClosed(TimeOnly time)
    {
        return time < T("09:00") ? MarketSessionPhase.PreOpen : MarketSessionPhase.Closed;
    }

    private static MarketSessionUpdateDto ApplyPhase(
        MarketSessionUpdateDto session,
        MarketSessionPhase phase,
        string source)
    {
        return session with
        {
            Phase = phase.Code,
            Label = phase.Label,
            IsOpen = phase.IsOpen,
            IsAuction = phase.IsAuction,
            IsContinuous = phase.IsContinuous,
            IsPutThrough = phase.IsPutThrough,
            IsAfterHours = phase.IsAfterHours,
            Source = source
        };
    }

    private static bool IsHnx(string productGroupId, string marketId)
    {
        return productGroupId.StartsWith("STX", StringComparison.Ordinal)
            || marketId.Contains("HNX", StringComparison.Ordinal);
    }

    private static bool IsUpcom(string productGroupId, string marketId)
    {
        return productGroupId.StartsWith("UPX", StringComparison.Ordinal)
            || marketId.Contains("UPC", StringComparison.Ordinal);
    }

    private static bool ContainsAny(string value, params string[] needles)
    {
        return needles.Any(needle => value.Contains(needle, StringComparison.Ordinal));
    }

    private static bool InRange(TimeOnly value, string from, string to)
    {
        return value >= T(from) && value < T(to);
    }

    private static TimeOnly T(string value)
    {
        return TimeOnly.ParseExact(value, "HH:mm");
    }

    private static TimeZoneInfo ResolveVietnamTimeZone()
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById("Asia/Ho_Chi_Minh");
        }
        catch (TimeZoneNotFoundException)
        {
            return TimeZoneInfo.FindSystemTimeZoneById("SE Asia Standard Time");
        }
    }

    private sealed record MarketSessionPhase(
        string Code,
        string Label,
        bool IsOpen,
        bool IsAuction = false,
        bool IsContinuous = false,
        bool IsPutThrough = false,
        bool IsAfterHours = false)
    {
        public static readonly MarketSessionPhase PreOpen = new(
            MarketSessionPhases.PreOpen,
            "Trước giờ",
            IsOpen: false);

        public static readonly MarketSessionPhase Ato = new(
            MarketSessionPhases.Ato,
            "ATO",
            IsOpen: true,
            IsAuction: true);

        public static readonly MarketSessionPhase Continuous = new(
            MarketSessionPhases.Continuous,
            "Liên tục",
            IsOpen: true,
            IsContinuous: true);

        public static readonly MarketSessionPhase LunchBreak = new(
            MarketSessionPhases.LunchBreak,
            "Nghỉ trưa",
            IsOpen: false);

        public static readonly MarketSessionPhase Atc = new(
            MarketSessionPhases.Atc,
            "ATC",
            IsOpen: true,
            IsAuction: true);

        public static readonly MarketSessionPhase Plo = new(
            MarketSessionPhases.Plo,
            "PLO sau giờ",
            IsOpen: true,
            IsAfterHours: true);

        public static readonly MarketSessionPhase PutThrough = new(
            MarketSessionPhases.PutThrough,
            "Thỏa thuận",
            IsOpen: true,
            IsPutThrough: true);

        public static readonly MarketSessionPhase Closed = new(
            MarketSessionPhases.Closed,
            "Đã đóng cửa",
            IsOpen: false);
    }
}
