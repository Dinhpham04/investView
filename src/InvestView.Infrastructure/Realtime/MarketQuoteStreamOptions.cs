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
