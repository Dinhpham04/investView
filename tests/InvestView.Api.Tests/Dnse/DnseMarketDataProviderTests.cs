using System.Text.Json;
using InvestView.Application.Abstractions.MarketData;
using InvestView.Infrastructure.Dnse;
using Microsoft.Extensions.Options;

namespace InvestView.Api.Tests.Dnse;

public sealed class DnseMarketDataProviderTests
{
    [Fact]
    public async Task GetMarketBoardAsync_LoadsSnapshotPartsAndMapsQuote()
    {
        var client = new FakeDnseMarketDataClient();
        var provider = new DnseMarketDataProvider(
            client,
            Options.Create(new DnseMarketDataOptions { DefaultSymbols = ["HPG"] }));

        var quotes = await provider.GetMarketBoardAsync(new MarketBoardQuery([], "G1"), CancellationToken.None);

        var quote = Assert.Single(quotes);
        Assert.Equal("HPG", quote.Symbol);
        Assert.Equal(29150m, quote.LastPrice);
        Assert.Equal(550m, quote.Change);
        Assert.Equal(2053706002, quote.ForeignRoom);
        Assert.Equal(3, quote.BidLevels.Count);
        Assert.Contains(client.Calls, call => call.Path == "/instruments");
        Assert.Contains(client.Calls, call => call.Path == "/price/HPG/secdef");
        Assert.Contains(client.Calls, call => call.Path == "/price/HPG/trades/latest");
        Assert.Contains(client.Calls, call => call.Path == "/price/HPG/quotes/latest");
        Assert.Contains(client.Calls, call => call.Path == "/price/HPG/foreign-trading");

        var foreignTradingCall = Assert.Single(client.Calls, call => call.Path == "/price/HPG/foreign-trading");
        Assert.NotNull(foreignTradingCall.Query);
        Assert.Equal("G1", foreignTradingCall.Query["boardId"]);
        Assert.Equal("1", foreignTradingCall.Query["limit"]);
        Assert.Equal("DESC", foreignTradingCall.Query["order"]);
        Assert.True(long.TryParse(foreignTradingCall.Query["from"], out var from));
        Assert.True(long.TryParse(foreignTradingCall.Query["to"], out var to));
        Assert.True(from < to);
    }

    [Fact]
    public async Task GetMarketBoardAsync_DuringVietnamTradingDay_QueriesForeignTradingFromStartOfVietnameseDay()
    {
        var client = new FakeDnseMarketDataClient();
        var now = new DateTimeOffset(2026, 7, 7, 3, 23, 0, TimeSpan.Zero);
        var provider = new DnseMarketDataProvider(
            client,
            Options.Create(new DnseMarketDataOptions { DefaultSymbols = ["HPG"] }),
            timeProvider: new FixedTimeProvider(now));

        await provider.GetMarketBoardAsync(new MarketBoardQuery([], "G1"), CancellationToken.None);

        var query = GetSingleForeignTradingQuery(client, "HPG");
        Assert.Equal(VietnamUnix(2026, 7, 7, 0, 0, 0), long.Parse(query["from"]!));
        Assert.Equal(now.ToUnixTimeSeconds(), long.Parse(query["to"]!));
    }

    [Fact]
    public async Task GetMarketBoardAsync_BeforeVietnamMarketOpen_QueriesPreviousBusinessDay()
    {
        var client = new FakeDnseMarketDataClient();
        var beforeOpenTuesday = new DateTimeOffset(2026, 7, 7, 1, 30, 0, TimeSpan.Zero);
        var provider = new DnseMarketDataProvider(
            client,
            Options.Create(new DnseMarketDataOptions { DefaultSymbols = ["HPG"] }),
            timeProvider: new FixedTimeProvider(beforeOpenTuesday));

        await provider.GetMarketBoardAsync(new MarketBoardQuery([], "G1"), CancellationToken.None);

        var query = GetSingleForeignTradingQuery(client, "HPG");
        Assert.Equal(VietnamUnix(2026, 7, 6, 0, 0, 0), long.Parse(query["from"]!));
        Assert.Equal(VietnamUnix(2026, 7, 6, 23, 59, 59), long.Parse(query["to"]!));
    }

    [Fact]
    public async Task GetMarketBoardAsync_BeforeVietnamMarketOpenOnMonday_QueriesPreviousFriday()
    {
        var client = new FakeDnseMarketDataClient();
        var beforeOpenMonday = new DateTimeOffset(2026, 7, 6, 1, 30, 0, TimeSpan.Zero);
        var provider = new DnseMarketDataProvider(
            client,
            Options.Create(new DnseMarketDataOptions { DefaultSymbols = ["HPG"] }),
            timeProvider: new FixedTimeProvider(beforeOpenMonday));

        await provider.GetMarketBoardAsync(new MarketBoardQuery([], "G1"), CancellationToken.None);

        var query = GetSingleForeignTradingQuery(client, "HPG");
        Assert.Equal(VietnamUnix(2026, 7, 3, 0, 0, 0), long.Parse(query["from"]!));
        Assert.Equal(VietnamUnix(2026, 7, 3, 23, 59, 59), long.Parse(query["to"]!));
    }

    [Fact]
    public async Task GetMarketBoardAsync_WhenMarketIdIsProvided_ResolvesSymbolsFromInstruments()
    {
        var client = new FakeDnseMarketDataClient();
        var provider = new DnseMarketDataProvider(
            client,
            Options.Create(new DnseMarketDataOptions { InstrumentPageSize = 100 }));

        var quotes = await provider.GetMarketBoardAsync(new MarketBoardQuery([], "G1", MarketId: "STO"), CancellationToken.None);

        Assert.Equal(["HPG"], quotes.Select(quote => quote.Symbol));
        var instrumentsCall = Assert.Single(client.Calls, call => call.Path == "/instruments");
        Assert.NotNull(instrumentsCall.Query);
        Assert.Equal("STO", instrumentsCall.Query["marketId"]);
        Assert.Equal("ST", instrumentsCall.Query["securityGroupId"]);
        Assert.Equal("100", instrumentsCall.Query["limit"]);
        Assert.Equal("1", instrumentsCall.Query["page"]);
        Assert.False(instrumentsCall.Query.ContainsKey("symbol") && instrumentsCall.Query["symbol"] is not null);
    }

    [Fact]
    public async Task GetMarketBoardAsync_WhenSymbolsAreCommaSeparated_LoadsEachSymbolSeparately()
    {
        var client = new FakeDnseMarketDataClient();
        var provider = new DnseMarketDataProvider(
            client,
            Options.Create(new DnseMarketDataOptions()));

        var quotes = await provider.GetMarketBoardAsync(new MarketBoardQuery(["HPG,SSI"], "G1"), CancellationToken.None);

        Assert.Equal(["HPG", "SSI"], quotes.Select(quote => quote.Symbol));
        Assert.Contains(client.Calls, call => call.Path == "/price/HPG/foreign-trading");
        Assert.Contains(client.Calls, call => call.Path == "/price/SSI/foreign-trading");
        Assert.DoesNotContain(client.Calls, call => call.Path.Contains("HPG,SSI", StringComparison.Ordinal));
    }

    [Fact]
    public async Task GetMarketBoardAsync_WhenIndexNameIsProvided_ResolvesSymbolsFromInstruments()
    {
        var client = new FakeDnseMarketDataClient();
        var provider = new DnseMarketDataProvider(
            client,
            Options.Create(new DnseMarketDataOptions { InstrumentPageSize = 100 }));

        var quotes = await provider.GetMarketBoardAsync(new MarketBoardQuery([], "G1", IndexName: "VN30"), CancellationToken.None);

        Assert.Equal(["HPG"], quotes.Select(quote => quote.Symbol));
        var instrumentsCall = Assert.Single(client.Calls, call => call.Path == "/instruments");
        Assert.NotNull(instrumentsCall.Query);
        Assert.Equal("VN30", instrumentsCall.Query["indexName"]);
        Assert.Equal("ST", instrumentsCall.Query["securityGroupId"]);
        Assert.Equal("100", instrumentsCall.Query["limit"]);
        Assert.Equal("1", instrumentsCall.Query["page"]);
        Assert.False(instrumentsCall.Query.ContainsKey("symbol") && instrumentsCall.Query["symbol"] is not null);
    }

    [Fact]
    public async Task GetMarketBoardAsync_WhenInstrumentPageIsFull_LoadsNextPage()
    {
        var client = new FakeDnseMarketDataClient();
        var provider = new DnseMarketDataProvider(
            client,
            Options.Create(new DnseMarketDataOptions { InstrumentPageSize = 1, MaxInstrumentPages = 5 }));

        var quotes = await provider.GetMarketBoardAsync(new MarketBoardQuery([], "G1", IndexName: "VN30"), CancellationToken.None);

        Assert.Equal(["HPG", "SSI"], quotes.Select(quote => quote.Symbol));
        Assert.Contains(client.Calls, call => call.Path == "/instruments" && call.Query?["page"] == "1");
        Assert.Contains(client.Calls, call => call.Path == "/instruments" && call.Query?["page"] == "2");
        Assert.Contains(client.Calls, call => call.Path == "/instruments" && call.Query?["page"] == "3");
    }

    private sealed class FakeDnseMarketDataClient : IDnseMarketDataClient
    {
        public List<(string Path, IReadOnlyDictionary<string, string?>? Query)> Calls { get; } = [];

        public Task<JsonDocument> GetJsonAsync(
            string path,
            IReadOnlyDictionary<string, string?>? query,
            CancellationToken cancellationToken)
        {
            Calls.Add((path, query));

            var json = path switch
            {
                "/instruments" when query?["limit"] == "1" && query["page"] == "1" => """{ "data": [{ "symbol": "HPG", "marketId": "HOSE", "name": "Hoa Phat Group" }] }""",
                "/instruments" when query?["limit"] == "1" && query["page"] == "2" => """{ "data": [{ "symbol": "SSI", "marketId": "HOSE", "name": "SSI Securities" }] }""",
                "/instruments" when query?["limit"] == "1" && query["page"] == "3" => """{ "data": [] }""",
                "/instruments" => """{ "data": [{ "symbol": "HPG", "marketId": "HOSE", "name": "Hoa Phat Group" }] }""",
                "/price/HPG/secdef" => """{ "basicPrice": 28600, "ceilingPrice": 30600, "floorPrice": 26600, "securityStatus": "Continuous" }""",
                "/price/HPG/trades/latest" => """{ "matchPrice": 29150, "matchQtty": 2500, "totalVolumeTraded": 12450000, "grossTradeAmount": 362917500000, "openPrice": 28700, "highestPrice": 29200, "lowestPrice": 28450, "time": "2026-07-03T07:45:00+00:00" }""",
                "/price/HPG/quotes/latest" => """{ "bid": [{ "price": 29100, "qtty": 18300 }, { "price": 29050, "qtty": 22500 }, { "price": 29000, "qtty": 41300 }], "offer": [{ "price": 29150, "qtty": 12000 }, { "price": 29200, "qtty": 17600 }, { "price": 29250, "qtty": 28400 }] }""",
                "/price/HPG/foreign-trading" when query?.ContainsKey("from") == true && query.ContainsKey("to") => """{ "foreigners": [{ "totalBuyVolume": 786100, "totalSellVolume": 1227649, "foreignerOrderLimitQuantity": 1742502798, "foreignerBuyPossibleQuantity": 2053706002 }] }""",
                "/price/HPG/foreign-trading" => throw new InvalidOperationException("foreign-trading requires from/to query params."),
                "/price/SSI/secdef" => """{ "basicPrice": 35200, "ceilingPrice": 37650, "floorPrice": 32750, "securityStatus": "Continuous" }""",
                "/price/SSI/trades/latest" => """{ "matchPrice": 34850, "matchQtty": 1800, "totalVolumeTraded": 7820000, "grossTradeAmount": 272527000000, "openPrice": 35400, "highestPrice": 35600, "lowestPrice": 34700, "time": "2026-07-03T07:45:00+00:00" }""",
                "/price/SSI/quotes/latest" => """{ "bid": [{ "price": 34800, "qtty": 15400 }], "offer": [{ "price": 34850, "qtty": 9400 }] }""",
                "/price/SSI/foreign-trading" when query?.ContainsKey("from") == true && query.ContainsKey("to") => """{ "foreigners": [{ "totalBuyVolume": 2410791, "totalSellVolume": 1038440, "foreignerOrderLimitQuantity": 360456325, "foreignerBuyPossibleQuantity": 478123456 }] }""",
                "/price/SSI/foreign-trading" => throw new InvalidOperationException("foreign-trading requires from/to query params."),
                _ => throw new InvalidOperationException($"Unexpected DNSE path: {path}")
            };

            return Task.FromResult(JsonDocument.Parse(json));
        }
    }

    private static IReadOnlyDictionary<string, string?> GetSingleForeignTradingQuery(
        FakeDnseMarketDataClient client,
        string symbol)
    {
        var call = Assert.Single(client.Calls, call => call.Path == $"/price/{symbol}/foreign-trading");
        Assert.NotNull(call.Query);
        return call.Query;
    }

    private static long VietnamUnix(int year, int month, int day, int hour, int minute, int second)
    {
        return new DateTimeOffset(year, month, day, hour, minute, second, TimeSpan.FromHours(7)).ToUnixTimeSeconds();
    }

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow()
        {
            return utcNow;
        }
    }
}
