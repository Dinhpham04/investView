namespace InvestView.Infrastructure.MarketData;

public sealed class MarketStateOptions
{
    public const string SectionName = "MarketData:State";

    public string RedisConnectionString { get; set; } = string.Empty;
    public string RedisKeyPrefix { get; set; } = "investview";
    public string RedisEnvironment { get; set; } = "dev";
    public string RedisSchemaVersion { get; set; } = "v2";
    public string RedisChannelName { get; set; } = "investview:market-state-events";
    public TimeSpan LatestStateTtl { get; set; } = TimeSpan.FromHours(18);
    public TimeSpan QuoteStateTtl { get; set; } = TimeSpan.Zero;
    public TimeSpan SymbolDetailTtl { get; set; } = TimeSpan.FromDays(7);
    public TimeSpan LatestTradesTtl { get; set; } = TimeSpan.FromDays(3);
    public TimeSpan OhlcTtl { get; set; } = TimeSpan.FromDays(30);
    public TimeSpan MembershipTtl { get; set; } = TimeSpan.FromDays(7);
    public TimeSpan BackfillLockTtl { get; set; } = TimeSpan.FromSeconds(20);
}
