using InvestView.Domain.Trading;

namespace InvestView.Domain.Tests.Trading;

public sealed class UserAccountEntityTests
{
    [Fact]
    public void WatchlistGroup_NormalizesName()
    {
        var userId = Guid.NewGuid();

        var group = new WatchlistGroup(userId, "  TK H197731  ");

        Assert.Equal("TK H197731", group.Name);
        Assert.Equal(userId, group.UserId);
    }

    [Fact]
    public void WatchlistItem_NormalizesSymbolAndBoard()
    {
        var groupId = Guid.NewGuid();

        var item = new WatchlistItem(groupId, " hpg ", " g1 ");

        Assert.Equal("HPG", item.Symbol);
        Assert.Equal("G1", item.BoardId);
        Assert.Equal(groupId, item.GroupId);
    }

    [Fact]
    public void WatchlistItem_WhenSymbolContainsWhitespace_ShouldReject()
    {
        var groupId = Guid.NewGuid();

        Assert.Throws<ArgumentException>(() => new WatchlistItem(groupId, "HP G", "G1"));
    }

    [Fact]
    public void CashAccount_WhenCreatedWithNegativeBalance_ShouldReject()
    {
        var userId = Guid.NewGuid();

        Assert.Throws<ArgumentOutOfRangeException>(() => new CashAccount(userId, "VND", -1m, 0m));
    }

    [Fact]
    public void Holding_WhenAvailableQuantityExceedsQuantity_ShouldReject()
    {
        var userId = Guid.NewGuid();

        Assert.Throws<ArgumentOutOfRangeException>(() => new Holding(userId, "SSI", "G1", 100, 101, 30_000m));
    }

    [Fact]
    public void SimulatedOrder_WhenCreated_ShouldNormalizeMarketIdentity()
    {
        var userId = Guid.NewGuid();

        var order = new SimulatedOrder(userId, " vcb ", " g1 ", OrderSide.Buy, 1_000, 80_000m);

        Assert.Equal("VCB", order.Symbol);
        Assert.Equal("G1", order.BoardId);
        Assert.Equal(OrderStatus.New, order.Status);
        Assert.Equal(0, order.FilledQuantity);
    }

    [Fact]
    public void SimulatedOrder_WhenSideIsInvalid_ShouldReject()
    {
        var userId = Guid.NewGuid();

        Assert.Throws<ArgumentOutOfRangeException>(
            () => new SimulatedOrder(userId, "VCB", "G1", (OrderSide)999, 1_000, 80_000m));
    }
}
