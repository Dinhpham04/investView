using System.Text.Json;
using InvestView.Infrastructure.Dnse;

namespace InvestView.Api.Tests.Dnse;

public sealed class DnseMarketDataMapperTests
{
    [Fact]
    public void MapMarketQuote_NormalizesSnapshotPartsIntoInternalQuote()
    {
        using var instrument = JsonDocument.Parse(
            """
            {
              "symbol": "HPG",
              "marketId": "HOSE",
              "name": "Hoa Phat Group"
            }
            """);
        using var secdef = JsonDocument.Parse(
            """
            {
              "basicPrice": 28600,
              "ceilingPrice": 30600,
              "floorPrice": 26600,
              "securityStatus": "Continuous"
            }
            """);
        using var trade = JsonDocument.Parse(
            """
            {
              "matchPrice": 29150,
              "matchQtty": 2500,
              "totalVolumeTraded": 12450000,
              "grossTradeAmount": 362917500000,
              "openPrice": 28700,
              "highestPrice": 29200,
              "lowestPrice": 28450,
              "time": "2026-07-03T07:45:00+00:00"
            }
            """);
        using var quote = JsonDocument.Parse(
            """
            {
              "bid": [
                { "price": 29100, "quantity": 18300 },
                { "price": 29050, "quantity": 22500 },
                { "price": 29000, "quantity": 41300 }
              ],
              "offer": [
                { "price": 29150, "qtty": 12000 },
                { "price": 29200, "qtty": 17600 },
                { "price": 29250, "qtty": 28400 }
              ]
            }
            """);
        using var foreign = JsonDocument.Parse(
            """
            {
              "foreigners": [
                {
                  "marketId": "STO",
                  "boardId": "G1",
                  "symbol": "HPG",
                  "sellVolume": 3106100,
                  "buyVolume": 434700,
                  "totalSellVolume": 3106286,
                  "totalBuyVolume": 434895,
                  "foreignerOrderLimitQuantity": 1743355298,
                  "foreignerBuyPossibleQuantity": 2053706002,
                  "time": "2026-07-06 15:33:11.709"
                }
              ]
            }
            """);

        var result = DnseMarketDataMapper.MapMarketQuote(
            "HPG",
            "G1",
            instrument.RootElement,
            secdef.RootElement,
            trade.RootElement,
            quote.RootElement,
            foreign.RootElement,
            new DateTimeOffset(2026, 7, 3, 7, 46, 0, TimeSpan.Zero));

        Assert.Equal("HPG", result.Symbol);
        Assert.Equal("G1", result.BoardId);
        Assert.Equal("HOSE", result.MarketId);
        Assert.Equal("Hoa Phat Group", result.DisplayName);
        Assert.Equal(28600m, result.ReferencePrice);
        Assert.Equal(30600m, result.CeilingPrice);
        Assert.Equal(26600m, result.FloorPrice);
        Assert.Equal(29150m, result.LastPrice);
        Assert.Equal(550m, result.Change);
        Assert.Equal(1.92m, result.ChangePercent);
        Assert.Equal(2500, result.LastQuantity);
        Assert.Equal(12_450_000, result.TotalVolume);
        Assert.Equal(3, result.BidLevels.Count);
        Assert.Equal(29100m, result.BidLevels[0].Price);
        Assert.Equal(18300, result.BidLevels[0].Quantity);
        Assert.Equal(3, result.AskLevels.Count);
        Assert.Equal(434895, result.ForeignBuyVolume);
        Assert.Equal(3106286, result.ForeignSellVolume);
        Assert.Equal(2053706002, result.ForeignRoom);
        Assert.Equal(new DateTimeOffset(2026, 7, 3, 7, 45, 0, TimeSpan.Zero), result.UpdatedAt);
    }

    [Fact]
    public void MapMarketQuote_ScalesDnseTradeAndOrderQuantitiesButKeepsForeignVolumes()
    {
        using var trade = JsonDocument.Parse(
            """
            {
              "matchPrice": 29150,
              "changedValue": 0.55,
              "matchQtty": 2500,
              "totalVolumeTraded": 12450000,
              "grossTradeAmount": 362917500000
            }
            """);
        using var quote = JsonDocument.Parse(
            """
            {
              "bid": [{ "price": 29100, "qtty": 18300 }],
              "offer": [{ "price": 29150, "quantity": 12000 }]
            }
            """);
        using var foreign = JsonDocument.Parse(
            """
            {
              "foreigners": [
                {
                  "totalBuyVolume": 434895,
                  "totalSellVolume": 3106286,
                  "foreignerOrderLimitQuantity": 1743355298,
                  "foreignerBuyPossibleQuantity": 2053706002
                }
              ]
            }
            """);

        var result = DnseMarketDataMapper.MapMarketQuote(
            "HPG",
            "G1",
            null,
            null,
            trade.RootElement,
            quote.RootElement,
            foreign.RootElement,
            new DateTimeOffset(2026, 7, 3, 7, 46, 0, TimeSpan.Zero),
            quantityScaleFactor: 10);

        Assert.Equal(25_000, result.LastQuantity);
        Assert.Equal(550m, result.Change);
        Assert.Equal(124_500_000, result.TotalVolume);
        Assert.Equal(183_000, result.BidLevels[0].Quantity);
        Assert.Equal(120_000, result.AskLevels[0].Quantity);
        Assert.Equal(434_895, result.ForeignBuyVolume);
        Assert.Equal(3_106_286, result.ForeignSellVolume);
        Assert.Equal(2_053_706_002, result.ForeignRoom);
    }

    [Fact]
    public void MapMarketQuote_ScalesNegativePriceDelta()
    {
        using var trade = JsonDocument.Parse(
            """
            {
              "matchPrice": 34850,
              "changedValue": -0.35,
              "matchQtty": 1800
            }
            """);
        using var quote = JsonDocument.Parse("""{ "bid": [{ "price": 34800, "qtty": 15400 }] }""");

        var result = DnseMarketDataMapper.MapMarketQuote(
            "SSI",
            "G1",
            null,
            null,
            trade.RootElement,
            quote.RootElement,
            null,
            new DateTimeOffset(2026, 7, 3, 7, 46, 0, TimeSpan.Zero),
            quantityScaleFactor: 10);

        Assert.Equal(-350m, result.Change);
    }

    [Fact]
    public void MapMarketQuote_UnwrapsDnseNamedArrays()
    {
        using var trade = JsonDocument.Parse("""{ "trades": [{ "matchPrice": 29150, "matchQtty": 2500 }] }""");
        using var quote = JsonDocument.Parse(
            """
            {
              "quotes": [
                {
                  "bid": [{ "price": 29100, "qtty": 18300 }],
                  "offer": [{ "price": 29150, "qtty": 12000 }]
                }
              ]
            }
            """);
        using var foreign = JsonDocument.Parse(
            """
            {
              "foreigners": [
                {
                  "totalBuyVolume": 434895,
                  "totalSellVolume": 3106286,
                  "foreignerOrderLimitQuantity": 1743355298,
                  "foreignerBuyPossibleQuantity": 2053706002
                }
              ]
            }
            """);

        var result = DnseMarketDataMapper.MapMarketQuote(
            "HPG",
            "G1",
            null,
            null,
            trade.RootElement,
            quote.RootElement,
            foreign.RootElement,
            new DateTimeOffset(2026, 7, 6, 8, 0, 0, TimeSpan.Zero));

        Assert.Equal(29150m, result.LastPrice);
        Assert.Equal(2500, result.LastQuantity);
        Assert.Single(result.BidLevels);
        Assert.Single(result.AskLevels);
        Assert.Equal(434895, result.ForeignBuyVolume);
        Assert.Equal(3106286, result.ForeignSellVolume);
        Assert.Equal(2053706002, result.ForeignRoom);
    }

    [Fact]
    public void MapMarketQuote_SelectsForeignTradingRecordForRequestedSymbolAndBoard()
    {
        using var foreign = JsonDocument.Parse(
            """
            {
              "foreigners": [
                {
                  "symbol": "SSI",
                  "boardId": "G1",
                  "totalBuyVolume": 100,
                  "totalSellVolume": 200,
                  "foreignerBuyPossibleQuantity": 111111111,
                  "time": "2026-07-06 15:31:11.709"
                },
                {
                  "symbol": "HPG",
                  "boardId": "G2",
                  "totalBuyVolume": 300,
                  "totalSellVolume": 400,
                  "foreignerBuyPossibleQuantity": 222222222,
                  "time": "2026-07-06 15:32:11.709"
                },
                {
                  "symbol": "HPG",
                  "boardId": "G1",
                  "totalBuyVolume": 500,
                  "totalSellVolume": 600,
                  "foreignerBuyPossibleQuantity": 333333333,
                  "time": "2026-07-06 15:33:11.709"
                }
              ]
            }
            """);

        var result = DnseMarketDataMapper.MapMarketQuote(
            "HPG",
            "G1",
            null,
            null,
            null,
            null,
            foreign.RootElement,
            new DateTimeOffset(2026, 7, 6, 8, 0, 0, TimeSpan.Zero));

        Assert.Equal(500, result.ForeignBuyVolume);
        Assert.Equal(600, result.ForeignSellVolume);
        Assert.Equal(333333333, result.ForeignRoom);
    }

    [Fact]
    public void MapMarketQuote_SelectsLatestForeignTradingRecordWhenSymbolHasMultipleRecords()
    {
        using var foreign = JsonDocument.Parse(
            """
            {
              "foreigners": [
                {
                  "symbol": "HPG",
                  "boardId": "G1",
                  "totalBuyVolume": 100,
                  "totalSellVolume": 200,
                  "foreignerBuyPossibleQuantity": 111111111,
                  "time": "2026-07-06 15:31:11.709"
                },
                {
                  "symbol": "HPG",
                  "boardId": "G1",
                  "totalBuyVolume": 500,
                  "totalSellVolume": 600,
                  "foreignerBuyPossibleQuantity": 333333333,
                  "time": "2026-07-06 15:33:11.709"
                }
              ]
            }
            """);

        var result = DnseMarketDataMapper.MapMarketQuote(
            "HPG",
            "G1",
            null,
            null,
            null,
            null,
            foreign.RootElement,
            new DateTimeOffset(2026, 7, 6, 8, 0, 0, TimeSpan.Zero));

        Assert.Equal(500, result.ForeignBuyVolume);
        Assert.Equal(600, result.ForeignSellVolume);
        Assert.Equal(333333333, result.ForeignRoom);
    }

    [Fact]
    public void MapMarketQuote_NormalizesMixedPriceScalesBeforeCalculatingBoundariesAndChange()
    {
        using var secdef = JsonDocument.Parse(
            """
            {
              "data": {
                "securityDefinition": {
                  "basicPrice": 28.6,
                  "ceilingPrice": 30.6,
                  "floorPrice": 26.6
                }
              }
            }
            """);
        using var trade = JsonDocument.Parse(
            """
            {
              "matchPrice": 29150,
              "matchQtty": 2500,
              "highestPrice": 29200,
              "lowestPrice": 28450
            }
            """);
        using var quote = JsonDocument.Parse(
            """
            {
              "bid": [{ "price": 29100, "qtty": 18300 }],
              "offer": [{ "price": 29150, "qtty": 12000 }]
            }
            """);

        var result = DnseMarketDataMapper.MapMarketQuote(
            "HPG",
            "G1",
            null,
            secdef.RootElement,
            trade.RootElement,
            quote.RootElement,
            null,
            new DateTimeOffset(2026, 7, 6, 8, 0, 0, TimeSpan.Zero));

        Assert.Equal(28600m, result.ReferencePrice);
        Assert.Equal(30600m, result.CeilingPrice);
        Assert.Equal(26600m, result.FloorPrice);
        Assert.Equal(29150m, result.LastPrice);
        Assert.Equal(550m, result.Change);
        Assert.Equal(1.92m, result.ChangePercent);
    }

    [Fact]
    public void MapSymbolDetail_CombinesInstrumentSecdefAndSnapshotParts()
    {
        using var instrument = JsonDocument.Parse(
            """
            {
              "symbol": "HPG",
              "marketId": "HOSE",
              "name": "Hoa Phat Group",
              "organName": "Hoa Phat Group Joint Stock Company",
              "securityGroupId": "ST"
            }
            """);
        using var secdef = JsonDocument.Parse(
            """
            {
              "symbol": "HPG",
              "boardId": "G1",
              "isin": "VN000000HPG4",
              "productGrpId": "STOCK",
              "securityGroupId": "ST",
              "basicPrice": 28.6,
              "ceilingPrice": 30.6,
              "floorPrice": 26.6,
              "securityStatus": "Continuous",
              "symbolAdminStatusCode": "NORMAL",
              "symbolTradingMethodStatusCode": "NORMAL",
              "symbolTradingSanctionStatusCode": "NORMAL",
              "listingDate": "2007-11-15",
              "openInterestQuantity": 0
            }
            """);
        using var trade = JsonDocument.Parse(
            """
            {
              "symbol": "HPG",
              "boardId": "G1",
              "matchPrice": 29.15,
              "changedValue": 0.55,
              "changedPercent": 1.92,
              "matchQtty": 2500,
              "totalVolumeTraded": 12450000,
              "grossTradeAmount": 362917500000,
              "openPrice": 28.7,
              "highestPrice": 29.2,
              "lowestPrice": 28.45,
              "time": "2026-07-03T07:45:00+00:00"
            }
            """);
        using var quote = JsonDocument.Parse(
            """
            {
              "bid": [{ "price": 29.1, "qtty": 18300 }],
              "offer": [{ "price": 29.15, "qtty": 12000 }]
            }
            """);
        using var foreign = JsonDocument.Parse(
            """
            {
              "foreigners": [
                {
                  "symbol": "HPG",
                  "boardId": "G1",
                  "totalBuyVolume": 434895,
                  "totalSellVolume": 3106286,
                  "foreignerBuyPossibleQuantity": 2053706002
                }
              ]
            }
            """);

        var result = DnseMarketDataMapper.MapSymbolDetail(
            "HPG",
            "G1",
            instrument.RootElement,
            secdef.RootElement,
            trade.RootElement,
            quote.RootElement,
            foreign.RootElement,
            new DateTimeOffset(2026, 7, 3, 7, 46, 0, TimeSpan.Zero),
            quantityScaleFactor: 10);

        Assert.Equal("HPG", result.Symbol);
        Assert.Equal("VN000000HPG4", result.Isin);
        Assert.Equal("STOCK", result.ProductGroupId);
        Assert.Equal("ST", result.SecurityGroupId);
        Assert.Equal(28600m, result.ReferencePrice);
        Assert.Equal(30600m, result.CeilingPrice);
        Assert.Equal(26600m, result.FloorPrice);
        Assert.Equal(29150m, result.LastPrice);
        Assert.Equal(550m, result.Change);
        Assert.Equal(25_000, result.LastQuantity);
        Assert.Equal(124_500_000, result.TotalVolume);
        Assert.Equal(434_895, result.ForeignBuyVolume);
        Assert.Equal(2_053_706_002, result.ForeignRoom);
        Assert.Equal("NORMAL", result.SymbolAdminStatus);
        Assert.Equal(new DateTimeOffset(2007, 11, 15, 0, 0, 0, TimeSpan.Zero), result.ListingDate);
        Assert.Single(result.BidLevels);
        Assert.Single(result.AskLevels);
    }

    [Fact]
    public void MapOhlcBars_NormalizesDnseBarsIntoInternalBars()
    {
        using var ohlc = JsonDocument.Parse(
            """
            {
              "data": [
                { "symbol": "HPG", "resolution": "1", "open": 28.6, "high": 29.2, "low": 28.45, "close": 29.15, "volume": 12450000, "time": 1783079100 },
                { "symbol": "HPG", "resolution": "1", "open": 29.15, "high": 29.3, "low": 29.05, "close": 29.25, "volume": 5000, "time": 1783079160 }
              ]
            }
            """);

        var result = DnseMarketDataMapper.MapOhlcBars("HPG", "1", ohlc.RootElement, quantityScaleFactor: 10);

        Assert.Equal(2, result.Count);
        Assert.Equal("HPG", result[0].Symbol);
        Assert.Equal("1", result[0].Resolution);
        Assert.Equal(28600m, result[0].Open);
        Assert.Equal(29200m, result[0].High);
        Assert.Equal(28450m, result[0].Low);
        Assert.Equal(29150m, result[0].Close);
        Assert.Equal(124_500_000, result[0].Volume);
        Assert.True(result[0].Time < result[1].Time);
    }

    [Fact]
    public void MapLatestTrades_ScalesTradeQuantitiesAndSortsNewestFirst()
    {
        using var trades = JsonDocument.Parse(
            """
            {
              "trades": [
                {
                  "symbol": "HPG",
                  "boardId": "G1",
                  "matchPrice": 29.15,
                  "changedValue": 0.55,
                  "changedPercent": 1.92,
                  "matchQtty": 2500,
                  "totalVolumeTraded": 12450000,
                  "grossTradeAmount": 362917500000,
                  "side": 1,
                  "time": "2026-07-03T07:45:00+00:00"
                },
                {
                  "symbol": "HPG",
                  "boardId": "G1",
                  "matchPrice": 29.10,
                  "changedValue": 0.50,
                  "changedPercent": 1.75,
                  "matchQtty": 1800,
                  "totalVolumeTraded": 12447500,
                  "grossTradeAmount": 362844625000,
                  "side": 2,
                  "time": "2026-07-03T07:44:30+00:00"
                },
                {
                  "symbol": "SSI",
                  "boardId": "G1",
                  "matchPrice": 34.85,
                  "matchQtty": 100,
                  "time": "2026-07-03T07:46:00+00:00"
                }
              ]
            }
            """);

        var result = DnseMarketDataMapper.MapLatestTrades(
            "HPG",
            "G1",
            trades.RootElement,
            new DateTimeOffset(2026, 7, 3, 7, 46, 0, TimeSpan.Zero),
            quantityScaleFactor: 10);

        Assert.Equal(2, result.Count);
        Assert.Equal(29150m, result[0].Price);
        Assert.Equal(550m, result[0].Change);
        Assert.Equal(1.92m, result[0].ChangePercent);
        Assert.Equal(25_000, result[0].Quantity);
        Assert.Equal(124_500_000, result[0].TotalVolume);
        Assert.Equal("1", result[0].Side);
        Assert.True(result[0].Time > result[1].Time);
    }

    [Fact]
    public void MapLatestTrades_DoesNotScaleAlreadyNormalizedPriceDelta()
    {
        using var trades = JsonDocument.Parse(
            """
            {
              "trades": [
                {
                  "symbol": "HPG",
                  "boardId": "G1",
                  "matchPrice": 29150,
                  "changedValue": 50,
                  "matchQtty": 2500,
                  "time": "2026-07-03T07:45:00+00:00"
                }
              ]
            }
            """);

        var result = DnseMarketDataMapper.MapLatestTrades(
            "HPG",
            "G1",
            trades.RootElement,
            new DateTimeOffset(2026, 7, 3, 7, 46, 0, TimeSpan.Zero));

        var trade = Assert.Single(result);
        Assert.Equal(50m, trade.Change);
    }
}
