namespace InvestView.Infrastructure.Realtime;

public sealed class MarketQuoteStreamOptions
{
    public const string SectionName = "MarketData:QuoteStream";
    public const string MockSourceProvider = "Mock";
    public const string ConfiguredSourceProvider = "Configured";
    public const string DnseWebSocketSourceProvider = "DnseWebSocket";

    public bool Enabled { get; set; }

    public string SourceProvider { get; set; } = MockSourceProvider;

    public string[] Symbols { get; set; } = ["HPG", "SSI", "VCB"];

    public string BoardId { get; set; } = "G1";

    public int IntervalMilliseconds { get; set; } = 1_000;

    public MarketQuoteStreamScheduleOptions Schedule { get; set; } = new();

    public bool UsesMockCompatibleSourceProvider()
    {
        return SourceProvider.Equals(MockSourceProvider, StringComparison.OrdinalIgnoreCase)
            || SourceProvider.Equals(ConfiguredSourceProvider, StringComparison.OrdinalIgnoreCase);
    }

    public bool UsesDnseWebSocketSourceProvider()
    {
        return SourceProvider.Equals(DnseWebSocketSourceProvider, StringComparison.OrdinalIgnoreCase);
    }
}

public sealed class MarketQuoteStreamScheduleOptions
{
    public bool Enabled { get; set; } = true;

    public bool RequireActiveSubscriptions { get; set; } = true;

    public string TimeZoneId { get; set; } = "Asia/Ho_Chi_Minh";

    public TimeSpan ConnectStartLocalTime { get; set; } = new(7, 50, 0);

    public TimeSpan ConnectEndLocalTime { get; set; } = new(15, 30, 0);

    public string[] ActiveDays { get; set; } =
    [
        nameof(DayOfWeek.Monday),
        nameof(DayOfWeek.Tuesday),
        nameof(DayOfWeek.Wednesday),
        nameof(DayOfWeek.Thursday),
        nameof(DayOfWeek.Friday)
    ];

    public int RecheckIntervalSeconds { get; set; } = 60;
}
