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
}
