namespace InvestView.Infrastructure.MarketData;

public sealed class MarketDataCacheOptions
{
    public const string SectionName = "MarketData:Cache";

    public TimeSpan MarketBoardTtl { get; set; } = TimeSpan.FromSeconds(2);

    public TimeSpan SymbolDetailTtl { get; set; } = TimeSpan.FromMinutes(30);

    public TimeSpan OhlcTtl { get; set; } = TimeSpan.FromMinutes(5);

    public TimeSpan LatestTradesTtl { get; set; } = TimeSpan.FromSeconds(2);
}
