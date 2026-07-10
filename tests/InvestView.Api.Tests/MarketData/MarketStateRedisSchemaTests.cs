using InvestView.Application.Dtos.MarketData;
using InvestView.Infrastructure.MarketData;
using StackExchange.Redis;

namespace InvestView.Api.Tests.MarketData;

public sealed class MarketStateRedisSchemaTests
{
    [Fact]
    public void QuoteStateKey_UsesVersionedAggregateKeyWithClusterHashTag()
    {
        var schema = CreateSchema();

        var key = schema.QuoteStateKey("g1", "hpg").ToString();

        Assert.Equal("investview:paper:md:v2:quote:{G1:HPG}:state", key);
    }

    [Fact]
    public void ToQuoteHash_MaterializesPayloadScalarFieldsAndGroupTimestamps()
    {
        var schema = CreateSchema();
        var quote = CreateQuote();

        var entries = schema
            .ToQuoteHash(quote, includeGroupTimestamps: true)
            .ToDictionary(entry => entry.Name.ToString(), entry => entry.Value.ToString());

        Assert.Equal("HPG", entries["symbol"]);
        Assert.Equal("G1", entries["boardId"]);
        Assert.Equal("STO", entries["marketId"]);
        Assert.Equal("23.5", entries["referencePrice"]);
        Assert.Equal("24", entries["lastPrice"]);
        Assert.Equal("100", entries["foreignBuyVolume"]);
        Assert.Equal("90", entries["foreignSellVolume"]);
        Assert.Equal("1000000", entries["foreignRoom"]);
        Assert.True(entries.ContainsKey("payload"));
        Assert.True(entries.ContainsKey("referenceUpdatedAt"));
        Assert.True(entries.ContainsKey("priceUpdatedAt"));
        Assert.True(entries.ContainsKey("foreignUpdatedAt"));

        var roundTripped = schema.QuoteFromHash(entries.Select(entry => new HashEntry(entry.Key, entry.Value)).ToArray());
        Assert.NotNull(roundTripped);
        Assert.Equal(quote.Symbol, roundTripped.Symbol);
        Assert.Equal(quote.LastPrice, roundTripped.LastPrice);
        Assert.Equal(quote.ForeignRoom, roundTripped.ForeignRoom);
    }

    [Fact]
    public void ToQuoteHash_MaterializesExpectedAuctionFields()
    {
        var schema = CreateSchema();
        var quote = CreateQuote() with
        {
            ExpectedPrice = 24.2m,
            ExpectedQuantity = 1_000
        };

        var entries = schema
            .ToQuoteHash(quote, includeGroupTimestamps: true)
            .ToDictionary(entry => entry.Name.ToString(), entry => entry.Value.ToString());

        Assert.Equal("24.2", entries["expectedPrice"]);
        Assert.Equal("1000", entries["expectedQuantity"]);
        Assert.True(entries.ContainsKey("expectedUpdatedAt"));

        var roundTripped = schema.QuoteFromHash(entries.Select(entry => new HashEntry(entry.Key, entry.Value)).ToArray());
        Assert.NotNull(roundTripped);
        Assert.Equal(24.2m, roundTripped.ExpectedPrice);
        Assert.Equal(1_000, roundTripped.ExpectedQuantity);
    }

    [Fact]
    public void ToQuoteGroupTimestampHash_OnlyTouchesGroupsPresentInPartialUpdate()
    {
        var schema = CreateSchema();
        var update = new MarketQuoteUpdateDto(
            "HPG",
            "G1",
            LastPrice: 24.1m,
            Change: 0.6m,
            ChangePercent: 2.55m,
            LastQuantity: 1000,
            TotalVolume: 10_000,
            TotalValue: 241_000_000m,
            ForeignBuyVolume: null,
            ForeignSellVolume: null,
            ForeignRoom: null,
            BidLevels: null,
            AskLevels: null,
            TradingStatus: null,
            UpdatedAt: new DateTimeOffset(2026, 7, 9, 3, 1, 0, TimeSpan.Zero));

        var entries = schema
            .ToQuoteGroupTimestampHash(update)
            .Select(entry => (string)entry.Name!)
            .ToArray();

        Assert.Contains("updatedAt", entries);
        Assert.Contains("priceUpdatedAt", entries);
        Assert.DoesNotContain("depthUpdatedAt", entries);
        Assert.DoesNotContain("foreignUpdatedAt", entries);
        Assert.DoesNotContain("statusUpdatedAt", entries);
    }

    [Fact]
    public void ToQuoteGroupTimestampHash_WhenForeignUpdate_TouchesOnlyForeignGroup()
    {
        var schema = CreateSchema();
        var update = new MarketQuoteUpdateDto(
            "HPG",
            "G1",
            LastPrice: null,
            Change: null,
            ChangePercent: null,
            LastQuantity: null,
            TotalVolume: null,
            TotalValue: null,
            ForeignBuyVolume: 100,
            ForeignSellVolume: 90,
            ForeignRoom: 1_000_000,
            BidLevels: null,
            AskLevels: null,
            TradingStatus: null,
            UpdatedAt: new DateTimeOffset(2026, 7, 9, 3, 1, 0, TimeSpan.Zero));

        var entries = schema
            .ToQuoteGroupTimestampHash(update)
            .Select(entry => (string)entry.Name!)
            .ToArray();

        Assert.Contains("updatedAt", entries);
        Assert.Contains("foreignUpdatedAt", entries);
        Assert.DoesNotContain("priceUpdatedAt", entries);
        Assert.DoesNotContain("depthUpdatedAt", entries);
        Assert.DoesNotContain("statusUpdatedAt", entries);
    }

    [Fact]
    public void ToQuoteGroupTimestampHash_WhenExpectedPriceUpdate_TouchesOnlyExpectedGroup()
    {
        var schema = CreateSchema();
        var update = new MarketQuoteUpdateDto(
            "HPG",
            "G1",
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
            TradingStatus: null,
            UpdatedAt: new DateTimeOffset(2026, 7, 9, 2, 15, 0, TimeSpan.Zero),
            ExpectedPrice: 24.2m,
            ExpectedQuantity: 1_000);

        var entries = schema
            .ToQuoteGroupTimestampHash(update)
            .Select(entry => (string)entry.Name!)
            .ToArray();

        Assert.Contains("updatedAt", entries);
        Assert.Contains("expectedUpdatedAt", entries);
        Assert.DoesNotContain("priceUpdatedAt", entries);
        Assert.DoesNotContain("depthUpdatedAt", entries);
        Assert.DoesNotContain("foreignUpdatedAt", entries);
        Assert.DoesNotContain("statusUpdatedAt", entries);
    }

    [Fact]
    public void ToIndexHash_MaterializesEstimatedIndexFields()
    {
        var schema = CreateSchema();
        var estimatedUpdatedAt = new DateTimeOffset(2026, 7, 9, 2, 45, 0, TimeSpan.Zero);
        var index = CreateIndex() with
        {
            EstimatedValue = 1841.5m,
            EstimatedChange = 4.5m,
            EstimatedChangePercent = 0.24m,
            EstimatedTotalVolume = 110_000,
            EstimatedTotalValue = 1_100_000m,
            EstimatedUpdatedAt = estimatedUpdatedAt
        };

        var entries = schema
            .ToIndexHash(index)
            .ToDictionary(entry => entry.Name.ToString(), entry => entry.Value.ToString());

        Assert.Equal("1841.5", entries["estimatedValue"]);
        Assert.Equal("4.5", entries["estimatedChange"]);
        Assert.Equal("0.24", entries["estimatedChangePercent"]);
        Assert.Equal("110000", entries["estimatedTotalVolume"]);
        Assert.Equal("1100000", entries["estimatedTotalValue"]);
        Assert.Equal(estimatedUpdatedAt.ToString("O"), entries["estimatedUpdatedAt"]);

        var roundTripped = schema.IndexFromHash(entries.Select(entry => new HashEntry(entry.Key, entry.Value)).ToArray());
        Assert.NotNull(roundTripped);
        Assert.Equal(1841.5m, roundTripped.EstimatedValue);
        Assert.Equal(110_000, roundTripped.EstimatedTotalVolume);
    }

    [Fact]
    public void SessionStateKeyAndHash_UseProductGroupBoardSubject()
    {
        var schema = CreateSchema();
        var updatedAt = new DateTimeOffset(2026, 7, 9, 2, 15, 0, TimeSpan.Zero);
        var session = new MarketSessionUpdateDto("dvx", "g1", "sto", "ab2", "40", updatedAt);

        var key = schema.SessionStateKey(session.ProductGroupId, session.BoardId).ToString();
        var entries = schema.ToSessionHash(session);
        var roundTripped = schema.SessionFromHash(entries);

        Assert.Equal("investview:paper:md:v2:session:{STO:G1}:state", key);
        Assert.NotNull(roundTripped);
        Assert.Equal("dvx", roundTripped.MarketId);
        Assert.Equal("g1", roundTripped.BoardId);
        Assert.Equal("sto", roundTripped.ProductGroupId);
        Assert.Equal("ab2", roundTripped.EventId);
        Assert.Equal("40", roundTripped.TradingSessionId);
        Assert.Equal(updatedAt, roundTripped.UpdatedAt);
    }

    [Fact]
    public void OhlcCoverageToken_UsesUnixSecondRange()
    {
        var schema = CreateSchema();
        var from = new DateTimeOffset(2026, 7, 9, 2, 0, 0, TimeSpan.Zero);
        var to = new DateTimeOffset(2026, 7, 9, 3, 0, 0, TimeSpan.Zero);

        var token = schema.CoverageToken(from, to).ToString();

        Assert.Equal($"{from.ToUnixTimeSeconds()}:{to.ToUnixTimeSeconds()}", token);
    }

    private static MarketStateRedisSchema CreateSchema()
    {
        return new MarketStateRedisSchema(new MarketStateOptions
        {
            RedisKeyPrefix = "investview",
            RedisEnvironment = "paper",
            RedisSchemaVersion = "v2"
        });
    }

    private static MarketQuoteDto CreateQuote()
    {
        return new MarketQuoteDto(
            "HPG",
            "G1",
            "STO",
            "Hoa Phat Group",
            23.5m,
            25.1m,
            21.85m,
            24m,
            0.5m,
            2.13m,
            1000,
            10_000,
            240_000_000m,
            100,
            90,
            1_000_000,
            23.7m,
            24.1m,
            23.4m,
            [new PriceLevelDto(23.95m, 1000)],
            [new PriceLevelDto(24.05m, 1000)],
            "NO_HALT",
            new DateTimeOffset(2026, 7, 9, 3, 0, 0, TimeSpan.Zero));
    }

    private static MarketIndexDto CreateIndex()
    {
        return new MarketIndexDto(
            "VN30",
            Value: 1840m,
            Change: 3m,
            ChangePercent: 0.16m,
            ReferenceValue: 1837m,
            HighValue: 1845m,
            LowValue: 1835m,
            TotalVolume: 100_000,
            TotalValue: 1_000_000m,
            UpCount: 15,
            DownCount: 10,
            NoChangeCount: 5,
            CeilingCount: 1,
            FloorCount: 0,
            MarketId: "HOSE",
            TradingSessionId: "99",
            UpdatedAt: new DateTimeOffset(2026, 7, 9, 3, 0, 0, TimeSpan.Zero));
    }
}
