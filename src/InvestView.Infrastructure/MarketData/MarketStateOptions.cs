namespace InvestView.Infrastructure.MarketData;

public sealed class MarketStateOptions
{
    public const string SectionName = "MarketData:State";

    public string RedisConnectionString { get; set; } = string.Empty;
    public string RedisKeyPrefix { get; set; } = "investview";
    public string RedisChannelName { get; set; } = "investview:market-state-events";
    public TimeSpan LatestStateTtl { get; set; } = TimeSpan.FromHours(18);
}
