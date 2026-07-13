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
    public void Map_WhenMessageIsOhlc_ReturnsRealtimeOhlcUpdate()
    {
        var mapper = new DnseWebSocketMessageMapper(new FixedTimeProvider(FallbackTime));

        var message = mapper.Map(
            """
            {
              "T": "b",
              "symbol": "SSI",
              "resolution": "1",
              "open": 35.0,
              "high": 35.2,
              "low": 34.9,
              "close": 35.1,
              "volume": 12345,
              "type": "STOCK",
              "time": 1783479600,
              "lastUpdated": 1783479660000
            }
            """);

        Assert.Equal(DnseWebSocketMessageKind.OhlcUpdate, message.Kind);
        Assert.NotNull(message.OhlcUpdate);
        Assert.Equal("SSI", message.OhlcUpdate.Symbol);
        Assert.Equal("1", message.OhlcUpdate.Resolution);
        Assert.Equal(35_000m, message.OhlcUpdate.Open);
        Assert.Equal(35_100m, message.OhlcUpdate.Close);
        Assert.Equal(123_450, message.OhlcUpdate.Volume);
        Assert.False(message.OhlcUpdate.IsClosed);
        Assert.Equal(DateTimeOffset.FromUnixTimeSeconds(1_783_479_600), message.OhlcUpdate.Time);
        Assert.Equal(DateTimeOffset.FromUnixTimeMilliseconds(1_783_479_660_000), message.OhlcUpdate.UpdatedAt);
    }

    [Fact]
    public void Map_WhenMessageIsClosedOhlc_ReturnsClosedOhlcUpdate()
    {
        var mapper = new DnseWebSocketMessageMapper(new FixedTimeProvider(FallbackTime));

        var message = mapper.Map(
            """
            {
              "T": "bc",
              "symbol": "VN30",
              "resolution": "1D",
              "open": 1840.0,
              "high": 1850.0,
              "low": 1838.0,
              "close": 1848.5,
              "volume": 1234567,
              "type": "INDEX",
              "time": "2026-07-08T08:00:00Z"
            }
            """);

        Assert.Equal(DnseWebSocketMessageKind.OhlcUpdate, message.Kind);
        Assert.NotNull(message.OhlcUpdate);
        Assert.Equal("VN30", message.OhlcUpdate.Symbol);
        Assert.Equal("INDEX", message.OhlcUpdate.Type);
        Assert.True(message.OhlcUpdate.IsClosed);
        Assert.Equal(12_345_670, message.OhlcUpdate.Volume);
    }

    [Fact]
    public void Map_WhenIndexOhlcIsBelowThousand_DoesNotApplyStockPriceScale()
    {
        var mapper = new DnseWebSocketMessageMapper(new FixedTimeProvider(FallbackTime));

        var message = mapper.Map(
            """
            {
              "T": "b",
              "symbol": "HNX",
              "resolution": "1",
              "open": 292.10,
              "high": 293.25,
              "low": 291.90,
              "close": 292.57,
              "volume": 1000,
              "type": "INDEX",
              "time": 1783479600
            }
            """);

        Assert.Equal(DnseWebSocketMessageKind.OhlcUpdate, message.Kind);
        Assert.NotNull(message.OhlcUpdate);
        Assert.Equal("HNX", message.OhlcUpdate.Symbol);
        Assert.Equal("INDEX", message.OhlcUpdate.Type);
        Assert.Equal(292.10m, message.OhlcUpdate.Open);
        Assert.Equal(293.25m, message.OhlcUpdate.High);
        Assert.Equal(291.90m, message.OhlcUpdate.Low);
        Assert.Equal(292.57m, message.OhlcUpdate.Close);
    }

    [Fact]
    public void Map_WhenMessageIsExpectedPrice_ReturnsExpectedPriceQuoteUpdate()
    {
        var mapper = new DnseWebSocketMessageMapper(new FixedTimeProvider(FallbackTime));

        var message = mapper.Map(
            """
            {
              "T": "e",
              "marketId": "STO",
              "boardId": "G1",
              "symbol": "SSI",
              "closePrice": 35.0,
              "expectedTradePrice": 35.2,
              "expectedTradeQuantity": 1000,
              "time": "2026-07-08T02:15:00Z"
            }
            """);

        Assert.Equal(DnseWebSocketMessageKind.QuoteUpdate, message.Kind);
        Assert.NotNull(message.QuoteUpdate);
        Assert.Null(message.QuoteUpdate.LastPrice);
        Assert.Equal(35_200m, message.QuoteUpdate.ExpectedPrice);
        Assert.Equal(10_000, message.QuoteUpdate.ExpectedQuantity);
        Assert.Equal(DateTimeOffset.Parse("2026-07-08T02:15:00Z"), message.QuoteUpdate.UpdatedAt);
    }

    [Fact]
    public void Map_WhenMessageIsMarketIndex_ReturnsMarketIndexUpdate()
    {
        var mapper = new DnseWebSocketMessageMapper(new FixedTimeProvider(FallbackTime));

        var message = mapper.Map(
            """
            {
              "T": "mi",
              "indexName": "VNINDEX",
              "changedRatio": -0.70,
              "changedValue": -13.00,
              "fluctuationSteadinessIssueCount": 66,
              "fluctuationDownIssueCount": 206,
              "fluctuationUpIssueCount": 92,
              "fluctuationLowerLimitIssueCount": 3,
              "fluctuationUpperLimitIssueCount": 1,
              "lowestValueIndexes": 1831.25,
              "highestValueIndexes": 1857.00,
              "priorValueIndexes": 1853.70,
              "valueIndexes": 1840.70,
              "grossTradeAmount": 14603.675,
              "totalVolumeTraded": 585707000,
              "marketId": 1,
              "tradingSessionId": 99,
              "transactTime": "2026-07-03T07:45:00+00:00"
            }
            """);

        Assert.Equal(DnseWebSocketMessageKind.MarketIndexUpdate, message.Kind);
        Assert.NotNull(message.MarketIndexUpdate);
        Assert.Equal("VNINDEX", message.MarketIndexUpdate.IndexName);
        Assert.Equal(1840.70m, message.MarketIndexUpdate.Value);
        Assert.Equal(-13.00m, message.MarketIndexUpdate.Change);
        Assert.Equal(-0.70m, message.MarketIndexUpdate.ChangePercent);
        Assert.Equal(1853.70m, message.MarketIndexUpdate.ReferenceValue);
        Assert.Equal(585_707_000, message.MarketIndexUpdate.TotalVolume);
        Assert.Equal(14_603.675m, message.MarketIndexUpdate.TotalValue);
        Assert.Equal(92, message.MarketIndexUpdate.UpCount);
        Assert.Equal(206, message.MarketIndexUpdate.DownCount);
        Assert.Equal(66, message.MarketIndexUpdate.NoChangeCount);
        Assert.Equal("1", message.MarketIndexUpdate.MarketId);
        Assert.Equal("99", message.MarketIndexUpdate.TradingSessionId);
        Assert.Equal(DateTimeOffset.Parse("2026-07-03T07:45:00+00:00"), message.MarketIndexUpdate.UpdatedAt);
    }

    [Fact]
    public void Map_WhenMarketIndexGrossTradeAmountIsZero_UsesAccumulatedTradingValues()
    {
        var mapper = new DnseWebSocketMessageMapper(new FixedTimeProvider(FallbackTime));

        var message = mapper.Map(
            """
            {
              "T": "mi",
              "indexName": "VNINDEX",
              "valueIndexes": 1840.70,
              "grossTradeAmount": 0,
              "contauctAccTrdVal": 14000,
              "blkTrdAccTrdVal": 603.675,
              "totalVolumeTraded": 585707000,
              "transactTime": "2026-07-03T07:45:00+00:00"
            }
            """);

        Assert.Equal(DnseWebSocketMessageKind.MarketIndexUpdate, message.Kind);
        Assert.NotNull(message.MarketIndexUpdate);
        Assert.Equal("VNINDEX", message.MarketIndexUpdate.IndexName);
        Assert.Equal(14_603.675m, message.MarketIndexUpdate.TotalValue);
    }

    [Fact]
    public void Map_WhenMessageIsEstimatedMarketIndex_ReturnsEstimatedIndexFields()
    {
        var mapper = new DnseWebSocketMessageMapper(new FixedTimeProvider(FallbackTime));

        var message = mapper.Map(
            """
            {
              "T": "emi",
              "indexName": "VN30",
              "changedRatio": -0.20,
              "changedValue": -2.10,
              "valueIndexes": 1848.40,
              "grossTradeAmount": 6391.86,
              "totalVolumeTraded": 184907600,
              "time": "2026-07-08T06:56:29Z"
            }
            """);

        Assert.Equal(DnseWebSocketMessageKind.MarketIndexUpdate, message.Kind);
        Assert.NotNull(message.MarketIndexUpdate);
        Assert.Equal("VN30", message.MarketIndexUpdate.IndexName);
        Assert.Null(message.MarketIndexUpdate.Value);
        Assert.Equal(1848.40m, message.MarketIndexUpdate.EstimatedValue);
        Assert.Equal(-2.10m, message.MarketIndexUpdate.EstimatedChange);
        Assert.Equal(-0.20m, message.MarketIndexUpdate.EstimatedChangePercent);
        Assert.Equal(184_907_600, message.MarketIndexUpdate.EstimatedTotalVolume);
        Assert.Equal(6391.86m, message.MarketIndexUpdate.EstimatedTotalValue);
    }

    [Fact]
    public void Map_WhenEstimatedMarketIndexGrossTradeAmountIsZero_UsesAccumulatedTradingValues()
    {
        var mapper = new DnseWebSocketMessageMapper(new FixedTimeProvider(FallbackTime));

        var message = mapper.Map(
            """
            {
              "T": "emi",
              "indexName": "VN30",
              "valueIndexes": 1848.40,
              "grossTradeAmount": 0,
              "contauctAccTrdVal": 6000.00,
              "blkTrdAccTrdVal": 391.86,
              "totalVolumeTraded": 184907600,
              "time": "2026-07-08T06:56:29Z"
            }
            """);

        Assert.Equal(DnseWebSocketMessageKind.MarketIndexUpdate, message.Kind);
        Assert.NotNull(message.MarketIndexUpdate);
        Assert.Equal("VN30", message.MarketIndexUpdate.IndexName);
        Assert.Equal(6391.86m, message.MarketIndexUpdate.EstimatedTotalValue);
    }

    [Fact]
    public void Map_WhenMessageIsSession_ReturnsSessionUpdate()
    {
        var mapper = new DnseWebSocketMessageMapper(new FixedTimeProvider(FallbackTime));

        var message = mapper.Map(
            """
            {
              "T": "s",
              "marketId": "DVX",
              "boardId": "G1",
              "eventId": "AB2",
              "tradingSessionId": "40",
              "tscProdGrpId": "STO",
              "sendingTime": "2026-07-08T02:15:00Z"
            }
            """);

        Assert.Equal(DnseWebSocketMessageKind.MarketSessionUpdate, message.Kind);
        Assert.NotNull(message.MarketSessionUpdate);
        Assert.Equal("DVX", message.MarketSessionUpdate.MarketId);
        Assert.Equal("G1", message.MarketSessionUpdate.BoardId);
        Assert.Equal("STO", message.MarketSessionUpdate.ProductGroupId);
        Assert.Equal("AB2", message.MarketSessionUpdate.EventId);
        Assert.Equal("40", message.MarketSessionUpdate.TradingSessionId);
        Assert.Equal(DateTimeOffset.Parse("2026-07-08T02:15:00Z"), message.MarketSessionUpdate.UpdatedAt);
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
