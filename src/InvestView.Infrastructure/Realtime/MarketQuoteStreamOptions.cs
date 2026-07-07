namespace InvestView.Infrastructure.Realtime;

public sealed class MarketQuoteStreamOptions
{
    public const string SectionName = "MarketData:QuoteStream";

    public bool Enabled { get; set; }

    public string[] Symbols { get; set; } = ["HPG", "SSI", "VCB"];

    public string BoardId { get; set; } = "G1";

    public int IntervalMilliseconds { get; set; } = 1_000;
}
