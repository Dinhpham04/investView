namespace InvestView.Application.Dtos.MarketData;

public sealed record MarketSessionUpdateDto(
    string MarketId,
    string BoardId,
    string ProductGroupId,
    string EventId,
    string TradingSessionId,
    DateTimeOffset UpdatedAt,
    string Phase = MarketSessionPhases.Unknown,
    string Label = "Không xác định",
    bool IsOpen = false,
    bool IsAuction = false,
    bool IsContinuous = false,
    bool IsPutThrough = false,
    bool IsAfterHours = false,
    string Source = MarketSessionSources.Realtime);

public static class MarketSessionPhases
{
    public const string Unknown = "UNKNOWN";
    public const string PreOpen = "PRE_OPEN";
    public const string Ato = "ATO";
    public const string Continuous = "CONTINUOUS";
    public const string LunchBreak = "LUNCH_BREAK";
    public const string Atc = "ATC";
    public const string Plo = "PLO";
    public const string PutThrough = "PUT_THROUGH";
    public const string Closed = "CLOSED";
}

public static class MarketSessionSources
{
    public const string Realtime = "REALTIME";
    public const string ScheduleFallback = "SCHEDULE_FALLBACK";
}
