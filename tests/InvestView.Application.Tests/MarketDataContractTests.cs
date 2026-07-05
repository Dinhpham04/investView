using InvestView.Application;
using InvestView.Application.Abstractions.MarketData;
using InvestView.Application.Dtos.MarketData;

namespace InvestView.Application.Tests;

public sealed class MarketDataContractTests
{
    [Fact]
    public void MarketDataContracts_AreProviderNeutral()
    {
        var contractTypes = ApplicationAssembly.Marker.Assembly
            .GetTypes()
            .Where(type =>
                type.Namespace?.Contains(".MarketData", StringComparison.Ordinal) == true ||
                type == typeof(IMarketDataProvider) ||
                type == typeof(IMarketDataStream))
            .Select(type => type.Name);

        Assert.All(contractTypes, typeName => Assert.DoesNotContain("Dnse", typeName, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void MarketQuoteDto_ContainsSnapshotAndStreamReadyFields()
    {
        var quote = new MarketQuoteDto(
            Symbol: "HPG",
            BoardId: "G1",
            MarketId: "HOSE",
            DisplayName: "Hoa Phat Group",
            ReferencePrice: 28600m,
            CeilingPrice: 30600m,
            FloorPrice: 26600m,
            LastPrice: 29150m,
            Change: 550m,
            ChangePercent: 1.92m,
            LastQuantity: 2500,
            TotalVolume: 12_450_000,
            TotalValue: 362_917_500_000m,
            OpenPrice: 28700m,
            HighPrice: 29200m,
            LowPrice: 28450m,
            BidLevels: [new PriceLevelDto(29100m, 18300)],
            AskLevels: [new PriceLevelDto(29150m, 12000)],
            TradingStatus: "Continuous",
            UpdatedAt: new DateTimeOffset(2026, 7, 3, 7, 45, 0, TimeSpan.Zero));

        Assert.Equal("HPG", quote.Symbol);
        Assert.Equal("G1", quote.BoardId);
        Assert.Equal(28600m, quote.ReferencePrice);
        Assert.Equal(30600m, quote.CeilingPrice);
        Assert.Equal(26600m, quote.FloorPrice);
        Assert.Equal(29150m, quote.LastPrice);
        Assert.Equal(550m, quote.Change);
        Assert.Equal(1.92m, quote.ChangePercent);
        Assert.Equal(12_450_000, quote.TotalVolume);
        Assert.Single(quote.BidLevels);
        Assert.Single(quote.AskLevels);
        Assert.Equal(new DateTimeOffset(2026, 7, 3, 7, 45, 0, TimeSpan.Zero), quote.UpdatedAt);
    }
}
