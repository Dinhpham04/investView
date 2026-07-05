namespace InvestView.Infrastructure.MarketData;

public sealed class MarketDataCacheOptions
{
    public TimeSpan MarketBoardTtl { get; set; } = TimeSpan.FromSeconds(2);

    public TimeSpan SymbolDetailTtl { get; set; } = TimeSpan.FromMinutes(30);

    public TimeSpan OhlcTtl { get; set; } = TimeSpan.FromMinutes(5);
}
