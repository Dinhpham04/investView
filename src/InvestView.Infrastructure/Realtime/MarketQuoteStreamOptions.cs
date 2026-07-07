namespace InvestView.Infrastructure.Realtime;

public sealed class MarketQuoteStreamOptions
{
    public const string SectionName = "MarketData:QuoteStream";
    public const string MockSourceProvider = "Mock";
    public const string ConfiguredSourceProvider = "Configured";

    public bool Enabled { get; set; }

    public string SourceProvider { get; set; } = MockSourceProvider;

    public string[] Symbols { get; set; } = ["HPG", "SSI", "VCB"];

    public string BoardId { get; set; } = "G1";

    public int IntervalMilliseconds { get; set; } = 1_000;
}
