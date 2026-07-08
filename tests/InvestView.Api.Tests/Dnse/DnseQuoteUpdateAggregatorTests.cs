using InvestView.Application.Dtos.MarketData;
using InvestView.Infrastructure.Dnse;

namespace InvestView.Api.Tests.Dnse;

public sealed class DnseQuoteUpdateAggregatorTests
{
    [Fact]
    public void Apply_WhenReferenceIsKnown_ComputesChangeAndPercentForTradeUpdate()
    {
        var aggregator = new DnseQuoteUpdateAggregator();
        var updatedAt = new DateTimeOffset(2026, 7, 8, 3, 0, 0, TimeSpan.Zero);

        aggregator.Apply(new MarketQuoteUpdateDto(
            Symbol: "HPG",
            BoardId: "G1",
            LastPrice: null,
            Change: null,
            ChangePercent: null,
            LastQuantity: null,
            TotalVolume: null,
            TotalValue: null,
            ForeignBuyVolume: null,
            ForeignSellVolume: null,
            ForeignRoom: null,
            BidLevels: null,
            AskLevels: null,
            TradingStatus: "NO_HALT",
            UpdatedAt: updatedAt,
            ReferencePrice: 24.0m));

        var update = aggregator.Apply(new MarketQuoteUpdateDto(
            Symbol: "hpg",
            BoardId: "g1",
            LastPrice: 24.35m,
            Change: null,
            ChangePercent: null,
            LastQuantity: 40,
            TotalVolume: null,
            TotalValue: null,
            ForeignBuyVolume: null,
            ForeignSellVolume: null,
            ForeignRoom: null,
            BidLevels: null,
            AskLevels: null,
            TradingStatus: null,
            UpdatedAt: updatedAt.AddSeconds(1)));

        Assert.Equal(0.35m, update.Change);
        Assert.Equal(1.46m, update.ChangePercent);
        Assert.Null(update.ReferencePrice);
    }
}
