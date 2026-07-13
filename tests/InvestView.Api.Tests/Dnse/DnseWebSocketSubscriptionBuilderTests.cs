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
    [InlineData(MarketDataChannel.ExpectedPrice, "expected_price.G1.json")]
    [InlineData(MarketDataChannel.Ohlc, "ohlc.1.json")]
    [InlineData(MarketDataChannel.OhlcClosed, "ohlc_closed.1.json")]
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

    [Fact]
    public void BuildEstimatedMarketIndexSubscribePayload_UsesDnseEstimatedMarketIndexChannelNamesWithoutSymbols()
    {
        var payload = DnseWebSocketSubscriptionBuilder.BuildEstimatedMarketIndexSubscribePayload(
            indexNames: ["vn30", " VNINDEX "],
            encoding: "json");

        Assert.Equal(["estimated_market_index.VN30.json", "estimated_market_index.VNINDEX.json"], payload.Channels.Select(channel => channel.Name));
        Assert.All(payload.Channels, channel => Assert.Empty(channel.Symbols));
    }

    [Fact]
    public void BuildOhlcSubscribePayload_UsesResolutionChannelsWithSymbols()
    {
        var payload = DnseWebSocketSubscriptionBuilder.BuildOhlcSubscribePayload(
            symbols: ["ssi", "HPG"],
            resolutions: ["1", "1H"],
            encoding: "json",
            closed: true);

        Assert.Equal(["ohlc_closed.1.json", "ohlc_closed.1H.json"], payload.Channels.Select(channel => channel.Name));
        Assert.All(payload.Channels, channel => Assert.Equal(["HPG", "SSI"], channel.Symbols));
    }

    [Fact]
    public void BuildMarketIndexOhlcSubscribePayload_UsesOnlyOneMinuteResolution()
    {
        var openPayload = DnseWebSocketSubscriptionBuilder.BuildMarketIndexOhlcSubscribePayload(
            indexNames: ["vnindex", "VN30"],
            encoding: "json",
            closed: false);
        var closedPayload = DnseWebSocketSubscriptionBuilder.BuildMarketIndexOhlcSubscribePayload(
            indexNames: ["vnindex", "VN30"],
            encoding: "json",
            closed: true);

        Assert.Equal(["ohlc.1.json"], openPayload.Channels.Select(channel => channel.Name));
        Assert.Equal(["ohlc_closed.1.json"], closedPayload.Channels.Select(channel => channel.Name));
        Assert.All(openPayload.Channels, channel => Assert.Equal(["VN30", "VNINDEX"], channel.Symbols));
        Assert.All(closedPayload.Channels, channel => Assert.Equal(["VN30", "VNINDEX"], channel.Symbols));
    }

    [Fact]
    public void BuildOhlcUnsubscribePayload_UsesDnseUnsubscribeAction()
    {
        var payload = DnseWebSocketSubscriptionBuilder.BuildOhlcUnsubscribePayload(
            symbols: ["ssi"],
            resolutions: ["1d"],
            encoding: "json",
            closed: false);

        Assert.Equal("unsubscribe", payload.Action);
        Assert.Equal(["ohlc.1D.json"], payload.Channels.Select(channel => channel.Name));
        Assert.Equal(["SSI"], payload.Channels[0].Symbols);
    }

    [Fact]
    public void BuildSessionSubscribePayload_UsesProductGroupBoardChannelsWithoutSymbols()
    {
        var payload = DnseWebSocketSubscriptionBuilder.BuildSessionSubscribePayload(
            boardIds: ["g1", "g4"],
            productGroupId: "sto",
            encoding: "json");

        Assert.Equal(["session.STO.G1.json", "session.STO.G4.json"], payload.Channels.Select(channel => channel.Name));
        Assert.All(payload.Channels, channel => Assert.Empty(channel.Symbols));
    }
}
