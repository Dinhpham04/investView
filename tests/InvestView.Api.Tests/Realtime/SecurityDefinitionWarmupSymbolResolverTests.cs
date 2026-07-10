using System.Text.Json;
using InvestView.Infrastructure.Dnse;
using InvestView.Infrastructure.Realtime;
using Microsoft.Extensions.Options;

namespace InvestView.Api.Tests.Realtime;

public sealed class SecurityDefinitionWarmupSymbolResolverTests
{
    [Fact]
    public async Task ResolveAsync_PagesConfiguredMarketsWithStockFilter()
    {
        var client = new FakeDnseMarketDataClient();
        var resolver = new SecurityDefinitionWarmupSymbolResolver(
            client,
            Options.Create(new SecurityDefinitionWarmupOptions
            {
                MarketIds = ["STO", "STX", "UPX"],
                SecurityGroupId = "ST",
                InstrumentPageSize = 1,
                MaxInstrumentPages = 3
            }));

        var result = await resolver.ResolveAsync(CancellationToken.None);

        Assert.Equal(["BSR", "HPG", "SHS"], result.Symbols);
        Assert.Equal(["HPG"], result.SymbolsByMarket["STO"]);
        Assert.Equal(["SHS"], result.SymbolsByMarket["STX"]);
        Assert.Equal(["BSR"], result.SymbolsByMarket["UPX"]);
        Assert.Contains(client.Calls, call => call.Path == "/instruments" && call.Query?["marketId"] == "STO" && call.Query["page"] == "1");
        Assert.Contains(client.Calls, call => call.Path == "/instruments" && call.Query?["marketId"] == "STX" && call.Query["page"] == "1");
        Assert.Contains(client.Calls, call => call.Path == "/instruments" && call.Query?["marketId"] == "UPX" && call.Query["page"] == "1");
        Assert.All(client.Calls, call =>
        {
            Assert.NotNull(call.Query);
            Assert.Equal("ST", call.Query["securityGroupId"]);
            Assert.Equal("1", call.Query["limit"]);
        });
    }

    [Fact]
    public async Task ResolveAsync_DeduplicatesSymbolsAcrossMarkets()
    {
        var client = new FakeDnseMarketDataClient
        {
            OverrideJson = """{ "data": [{ "symbol": "hpg" }, { "symbol": " HPG " }] }"""
        };
        var resolver = new SecurityDefinitionWarmupSymbolResolver(
            client,
            Options.Create(new SecurityDefinitionWarmupOptions
            {
                MarketIds = ["STO"],
                InstrumentPageSize = 100,
                MaxInstrumentPages = 1
            }));

        var result = await resolver.ResolveAsync(CancellationToken.None);

        Assert.Equal(["HPG"], result.Symbols);
        Assert.Equal(["HPG"], result.SymbolsByMarket["STO"]);
    }

    private sealed class FakeDnseMarketDataClient : IDnseMarketDataClient
    {
        public List<(string Path, IReadOnlyDictionary<string, string?>? Query)> Calls { get; } = [];

        public string? OverrideJson { get; set; }

        public Task<JsonDocument> GetJsonAsync(
            string path,
            IReadOnlyDictionary<string, string?>? query,
            CancellationToken cancellationToken)
        {
            Calls.Add((path, query));

            if (OverrideJson is not null)
            {
                return Task.FromResult(JsonDocument.Parse(OverrideJson));
            }

            var json = query?["marketId"] switch
            {
                "STO" when query["page"] == "1" => """{ "data": [{ "symbol": "HPG" }] }""",
                "STO" => """{ "data": [] }""",
                "STX" when query["page"] == "1" => """{ "data": [{ "symbol": "SHS" }] }""",
                "STX" => """{ "data": [] }""",
                "UPX" when query["page"] == "1" => """{ "data": [{ "symbol": "BSR" }] }""",
                "UPX" => """{ "data": [] }""",
                _ => throw new InvalidOperationException($"Unexpected market: {query?["marketId"]}")
            };

            return Task.FromResult(JsonDocument.Parse(json));
        }
    }
}
