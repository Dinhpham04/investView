namespace InvestView.Infrastructure.MarketData;

public sealed class MarketDataProviderOptions
{
    public const string SectionName = "MarketData";

    public string Provider { get; set; } = "Mock";
}
