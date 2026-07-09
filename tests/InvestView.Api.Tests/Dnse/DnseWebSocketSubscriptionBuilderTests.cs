using InvestView.Application.Abstractions.MarketData;
using InvestView.Infrastructure.Dnse;

namespace InvestView.Api.Tests.Dnse;

public sealed class DnseWebSocketSubscriptionBuilderTests
{
    [Theory]
    [InlineData(MarketDataChannel.SecurityDefinition, "security_definition.G1.json")]
    [InlineData(MarketDataChannel.Trade, "tick.G1.json")]
    [InlineData(MarketDataChannel.TradeExtra, "tick_extra.G1.json")]
    [InlineData(MarketDataChannel.TopPrice, "top_price.G1.json")]
    [InlineData(MarketDataChannel.Foreign, "foreign.G1.json")]
    public void BuildChannelName_UsesDnseMarketDataChannelNames(MarketDataChannel channel, string expected)
    {
        var channelName = DnseWebSocketSubscriptionBuilder.BuildChannelName(channel, "g1", "json");

        Assert.Equal(expected, channelName);
    }

    [Fact]
    public void BuildChannelName_UsesProductGroupForSessionChannel()
    {
        var channelName = DnseWebSocketSubscriptionBuilder.BuildChannelName(
            MarketDataChannel.Session,
            boardId: "g1",
            encoding: "json",
            productGroupId: "sto");

        Assert.Equal("session.STO.G1.json", channelName);
    }

    [Fact]
    public void BuildSubscribePayload_NormalizesSymbolsAndChannels()
    {
        var payload = DnseWebSocketSubscriptionBuilder.BuildSubscribePayload(
            symbols: ["ssi", " hpg ", "SSI"],
            boardId: "g1",
            encoding: "json",
            channels: [MarketDataChannel.Trade, MarketDataChannel.TopPrice]);

        Assert.Equal("subscribe", payload.Action);
        Assert.Equal(["HPG", "SSI"], payload.Channels[0].Symbols);
        Assert.Equal("tick.G1.json", payload.Channels[0].Name);
        Assert.Equal("top_price.G1.json", payload.Channels[1].Name);
    }

    [Fact]
    public void BuildMarketIndexSubscribePayload_UsesDnseMarketIndexChannelNamesWithoutSymbols()
    {
        var payload = DnseWebSocketSubscriptionBuilder.BuildMarketIndexSubscribePayload(
            indexNames: ["vnindex", " VN30 ", "VNINDEX"],
            encoding: "json");

        Assert.Equal("subscribe", payload.Action);
        Assert.Equal(["market_index.VN30.json", "market_index.VNINDEX.json"], payload.Channels.Select(channel => channel.Name));
        Assert.All(payload.Channels, channel => Assert.Empty(channel.Symbols));
    }
}
