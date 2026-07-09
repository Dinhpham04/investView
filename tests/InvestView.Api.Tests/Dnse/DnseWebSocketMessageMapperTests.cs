using InvestView.Infrastructure.Dnse;

namespace InvestView.Api.Tests.Dnse;

public sealed class DnseWebSocketMessageMapperTests
{
    private static readonly DateTimeOffset FallbackTime = new(2026, 7, 8, 3, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Map_WhenMessageIsSecurityDefinition_ReturnsReferenceBandUpdate()
    {
        var mapper = new DnseWebSocketMessageMapper(new FixedTimeProvider(FallbackTime));

        var message = mapper.Map(
            """
            {
              "T": "sd",
              "marketId": "STO",
              "boardId": "G1",
              "symbol": "HPG",
              "basicPrice": 24.0,
              "ceilingPrice": 25.65,
              "floorPrice": 22.35,
              "securityStatus": "NO_HALT",
              "time": { "Seconds": 1783479600, "Nanos": 250000000 }
            }
            """);

        Assert.Equal(DnseWebSocketMessageKind.QuoteUpdate, message.Kind);
        Assert.NotNull(message.QuoteUpdate);
        Assert.Equal("HPG", message.QuoteUpdate.Symbol);
        Assert.Equal("G1", message.QuoteUpdate.BoardId);
        Assert.Equal(24_000m, message.QuoteUpdate.ReferencePrice);
        Assert.Equal(25_650m, message.QuoteUpdate.CeilingPrice);
        Assert.Equal(22_350m, message.QuoteUpdate.FloorPrice);
        Assert.Equal("NO_HALT", message.QuoteUpdate.TradingStatus);
        Assert.Equal(DateTimeOffset.FromUnixTimeSeconds(1_783_479_600).AddTicks(2_500_000), message.QuoteUpdate.UpdatedAt);
    }

    [Fact]
    public void Map_WhenMessageIsTrade_ReturnsMatchedPriceUpdate()
    {
        var mapper = new DnseWebSocketMessageMapper(new FixedTimeProvider(FallbackTime));

        var message = mapper.Map(
            """
            {
              "T": "t",
              "marketId": "STO",
              "boardId": "G1",
              "symbol": "HPG",
              "matchPrice": 24.35,
              "matchQtty": 40,
              "totalVolumeTraded": 1184240,
              "grossTradeAmount": 287.17458,
              "highestPrice": 24.35,
              "lowestPrice": 24.15,
              "openPrice": 24.25,
              "time": { "Seconds": 1783479700, "Nanos": 0 }
            }
            """);

        Assert.Equal(DnseWebSocketMessageKind.QuoteUpdate, message.Kind);
        Assert.NotNull(message.QuoteUpdate);
        Assert.Equal(24_350m, message.QuoteUpdate.LastPrice);
        Assert.Equal(400, message.QuoteUpdate.LastQuantity);
        Assert.Equal(11_842_400, message.QuoteUpdate.TotalVolume);
        Assert.Equal(287.17458m, message.QuoteUpdate.TotalValue);
        Assert.Equal(24_250m, message.QuoteUpdate.OpenPrice);
        Assert.Equal(24_350m, message.QuoteUpdate.HighPrice);
        Assert.Equal(24_150m, message.QuoteUpdate.LowPrice);
    }

    [Fact]
    public void Map_WhenMessageIsTradeExtra_ReturnsTradeUpdateWithSide()
    {
        var mapper = new DnseWebSocketMessageMapper(new FixedTimeProvider(FallbackTime));

        var message = mapper.Map(
            """
            {
              "T": "te",
              "marketId": "STO",
              "boardId": "G1",
              "symbol": "HPG",
              "matchPrice": 24.35,
              "matchQtty": 40,
              "side": "SELL",
              "totalVolumeTraded": 1184240,
              "grossTradeAmount": 287.17458,
              "time": { "Seconds": 1783479700, "Nanos": 100000000 }
            }
            """);

        Assert.Equal(DnseWebSocketMessageKind.TradeUpdate, message.Kind);
        Assert.NotNull(message.TradeUpdate);
        Assert.Equal("HPG", message.TradeUpdate.Symbol);
        Assert.Equal("G1", message.TradeUpdate.BoardId);
        Assert.Equal(24_350m, message.TradeUpdate.Price);
        Assert.Equal(400, message.TradeUpdate.Quantity);
        Assert.Equal(11_842_400, message.TradeUpdate.TotalVolume);
        Assert.Equal(287.17458m, message.TradeUpdate.TotalValue);
        Assert.Equal("S", message.TradeUpdate.Side);
        Assert.Equal(DateTimeOffset.FromUnixTimeSeconds(1_783_479_700).AddTicks(1_000_000), message.TradeUpdate.Time);
    }

    [Fact]
    public void Map_WhenMessageIsTopPrice_ReturnsBidAndAskLevels()
    {
        var mapper = new DnseWebSocketMessageMapper(new FixedTimeProvider(FallbackTime));

        var message = mapper.Map(
            """
            {
              "T": "q",
              "marketId": "STO",
              "boardId": "G1",
              "symbol": "HPG",
              "bid": [
                { "price": 24.30, "quantity": 500 },
                { "price": 24.25, "qtty": 700 }
              ],
              "offer": [
                { "price": 24.35, "quantity": 900 }
              ],
              "time": { "Seconds": 1783479800, "Nanos": 0 }
            }
            """);

        Assert.Equal(DnseWebSocketMessageKind.QuoteUpdate, message.Kind);
        Assert.NotNull(message.QuoteUpdate);
        Assert.Equal(2, message.QuoteUpdate.BidLevels!.Count);
        Assert.Equal(24_300m, message.QuoteUpdate.BidLevels[0].Price);
        Assert.Equal(5_000, message.QuoteUpdate.BidLevels[0].Quantity);
        Assert.Equal(7_000, message.QuoteUpdate.BidLevels[1].Quantity);
        var ask = Assert.Single(message.QuoteUpdate.AskLevels!);
        Assert.Equal(24_350m, ask.Price);
        Assert.Equal(9_000, ask.Quantity);
    }

    [Fact]
    public void Map_WhenMessageIsForeignInvestor_ReturnsDailyForeignTradingUpdate()
    {
        var mapper = new DnseWebSocketMessageMapper(new FixedTimeProvider(FallbackTime));

        var message = mapper.Map(
            """
            {
              "T": "f",
              "marketId": "STO",
              "boardId": "G1",
              "symbol": "HPG",
              "buyVolume": 434700,
              "sellVolume": 3106100,
              "totalBuyVolume": 434895,
              "totalSellVolume": 3106286,
              "foreignerBuyPossibleQuantity": 2053706002
            }
            """);

        Assert.Equal(DnseWebSocketMessageKind.QuoteUpdate, message.Kind);
        Assert.NotNull(message.QuoteUpdate);
        Assert.Equal(434_895, message.QuoteUpdate.ForeignBuyVolume);
        Assert.Equal(3_106_286, message.QuoteUpdate.ForeignSellVolume);
        Assert.Equal(2_053_706_002, message.QuoteUpdate.ForeignRoom);
        Assert.Equal(FallbackTime, message.QuoteUpdate.UpdatedAt);
    }

    [Fact]
    public void Map_WhenWebSocketPriceIsAlreadyInDong_DoesNotScaleAgain()
    {
        var mapper = new DnseWebSocketMessageMapper(new FixedTimeProvider(FallbackTime));

        var message = mapper.Map(
            """
            {
              "T": "t",
              "boardId": "G1",
              "symbol": "HPG",
              "matchPrice": 24350,
              "matchQtty": 40
            }
            """);

        Assert.NotNull(message.QuoteUpdate);
        Assert.Equal(24_350m, message.QuoteUpdate.LastPrice);
        Assert.Equal(400, message.QuoteUpdate.LastQuantity);
    }

    [Fact]
    public void Map_WhenMessageIsPing_ReturnsPingControlMessage()
    {
        var mapper = new DnseWebSocketMessageMapper(new FixedTimeProvider(FallbackTime));

        var message = mapper.Map("""{ "action": "ping" }""");

        Assert.Equal(DnseWebSocketMessageKind.Ping, message.Kind);
    }

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow()
        {
            return utcNow;
        }
    }
}
