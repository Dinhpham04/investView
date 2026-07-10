namespace InvestView.Infrastructure.Realtime;

public sealed class SecurityDefinitionWarmupOptions
{
    public const string SectionName = "MarketData:SecurityDefinitionWarmup";

    public bool Enabled { get; set; }

    public string BoardId { get; set; } = "G1";

    public string[] MarketIds { get; set; } = ["STO", "STX", "UPX"];

    public string SecurityGroupId { get; set; } = "ST";

    public int InstrumentPageSize { get; set; } = 100;

    public int MaxInstrumentPages { get; set; } = 20;

    public int SymbolBatchSize { get; set; } = 100;

    public int RunTimeoutSeconds { get; set; } = 1_200;

    public int RetryDelaySeconds { get; set; } = 60;

    public SecurityDefinitionWarmupScheduleOptions Schedule { get; set; } = new();
}

public sealed class SecurityDefinitionWarmupScheduleOptions
{
    public bool Enabled { get; set; } = true;

    public string TimeZoneId { get; set; } = "Asia/Ho_Chi_Minh";

    public TimeSpan StartLocalTime { get; set; } = new(7, 55, 0);

    public TimeSpan EndLocalTime { get; set; } = new(8, 15, 0);

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
