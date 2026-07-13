using InvestView.Infrastructure.Realtime;

namespace InvestView.Api.Tests.Realtime;

public sealed class MarketQuoteSubscriptionRegistryTests
{
    [Fact]
    public void SetConnectionSubscription_NormalizesAndDeduplicatesSymbols()
    {
        var registry = new MarketQuoteSubscriptionRegistry();

        var change = registry.SetConnectionSubscription(
            "connection-1",
            "g1",
            [" hpg ", "SSI", "hpg,vcb"]);

        Assert.Equal("G1", change.BoardId);
        Assert.Equal(["HPG", "SSI", "VCB"], change.Symbols);
        Assert.Equal(1, change.Snapshot.Version);
        Assert.Collection(
            change.Snapshot.Boards,
            board =>
            {
                Assert.Equal("G1", board.BoardId);
                Assert.Equal(["HPG", "SSI", "VCB"], board.Symbols);
            });
    }

    [Fact]
    public void SetConnectionSubscription_UnionsSymbolsAcrossConnections()
    {
        var registry = new MarketQuoteSubscriptionRegistry();

        registry.SetConnectionSubscription("connection-1", "G1", ["HPG", "SSI"]);
        var change = registry.SetConnectionSubscription("connection-2", "G1", ["SSI", "VCB"]);

        Assert.Collection(
            change.Snapshot.Boards,
            board =>
            {
                Assert.Equal("G1", board.BoardId);
                Assert.Equal(["HPG", "SSI", "VCB"], board.Symbols);
            });
    }

    [Fact]
    public void SetConnectionOhlcSubscription_NormalizesAndUnionsResolutionsBySymbol()
    {
        var registry = new MarketQuoteSubscriptionRegistry();

        registry.SetConnectionOhlcSubscription("connection-1", " hpg ", ["1d", "1", "1D"]);
        var snapshot = registry.SetConnectionOhlcSubscription("connection-2", "HPG", ["1h", "1"]);

        Assert.Empty(snapshot.Boards);
        Assert.Equal(2, snapshot.Version);
        Assert.Collection(
            snapshot.OhlcSubscriptions,
            subscription =>
            {
                Assert.Equal("HPG", subscription.Symbol);
                Assert.Equal(["1", "1D", "1H"], subscription.Resolutions);
            });
    }

    [Fact]
    public void SetConnectionOhlcSubscription_WhenCleared_RemovesOnlyOhlcDemand()
    {
        var registry = new MarketQuoteSubscriptionRegistry();

        registry.SetConnectionSubscription("connection-1", "G1", ["HPG"]);
        registry.SetConnectionOhlcSubscription("connection-1", "HPG", ["1D"]);

        var snapshot = registry.SetConnectionOhlcSubscription("connection-1", null, []);

        Assert.Collection(
            snapshot.Boards,
            board =>
            {
                Assert.Equal("G1", board.BoardId);
                Assert.Equal(["HPG"], board.Symbols);
            });
        Assert.Empty(snapshot.OhlcSubscriptions);
    }

    [Fact]
    public void RemoveConnection_RemovesOnlyThatConnectionsDemand()
    {
        var registry = new MarketQuoteSubscriptionRegistry();

        registry.SetConnectionSubscription("connection-1", "G1", ["HPG", "SSI"]);
        registry.SetConnectionSubscription("connection-2", "G1", ["SSI", "VCB"]);

        var snapshot = registry.RemoveConnection("connection-1");

        Assert.Collection(
            snapshot.Boards,
            board =>
            {
                Assert.Equal("G1", board.BoardId);
                Assert.Equal(["SSI", "VCB"], board.Symbols);
            });
    }

    [Fact]
    public void RemoveConnection_RemovesBoardAndOhlcDemandForThatConnection()
    {
        var registry = new MarketQuoteSubscriptionRegistry();

        registry.SetConnectionSubscription("connection-1", "G1", ["HPG"]);
        registry.SetConnectionOhlcSubscription("connection-1", "HPG", ["1D"]);
        registry.SetConnectionSubscription("connection-2", "G1", ["SSI"]);
        registry.SetConnectionOhlcSubscription("connection-2", "SSI", ["1"]);

        var snapshot = registry.RemoveConnection("connection-1");

        Assert.Collection(
            snapshot.Boards,
            board =>
            {
                Assert.Equal("G1", board.BoardId);
                Assert.Equal(["SSI"], board.Symbols);
            });
        Assert.Collection(
            snapshot.OhlcSubscriptions,
            subscription =>
            {
                Assert.Equal("SSI", subscription.Symbol);
                Assert.Equal(["1"], subscription.Resolutions);
            });
    }

    [Fact]
    public async Task WaitForChangeAsync_CompletesWhenActiveSymbolsChange()
    {
        var registry = new MarketQuoteSubscriptionRegistry();
        var initialSnapshot = registry.GetSnapshot();

        var waitTask = registry.WaitForChangeAsync(initialSnapshot.Version, CancellationToken.None).AsTask();
        registry.SetConnectionSubscription("connection-1", "G1", ["HPG"]);

        var snapshot = await waitTask;

        Assert.Equal(1, snapshot.Version);
        Assert.Collection(
            snapshot.Boards,
            board =>
            {
                Assert.Equal("G1", board.BoardId);
                Assert.Equal(["HPG"], board.Symbols);
            });
    }

    [Fact]
    public async Task WaitForChangeAsync_CompletesWhenOhlcDemandChanges()
    {
        var registry = new MarketQuoteSubscriptionRegistry();
        var initialSnapshot = registry.GetSnapshot();

        var waitTask = registry.WaitForChangeAsync(initialSnapshot.Version, CancellationToken.None).AsTask();
        registry.SetConnectionOhlcSubscription("connection-1", "HPG", ["1D"]);

        var snapshot = await waitTask;

        Assert.Equal(1, snapshot.Version);
        Assert.Collection(
            snapshot.OhlcSubscriptions,
            subscription =>
            {
                Assert.Equal("HPG", subscription.Symbol);
                Assert.Equal(["1D"], subscription.Resolutions);
            });
    }
}
