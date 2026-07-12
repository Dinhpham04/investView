using InvestView.Domain.Trading;

namespace InvestView.Domain.Tests.Trading;

public sealed class SimulatedTradingEntityTests
{
    [Fact]
    public void CashAccount_Debit_WhenBalanceIsSufficient_ShouldDecreaseBalance()
    {
        var account = new CashAccount(Guid.NewGuid(), "vnd", 1_000_000m, 1_000_000m);

        account.Debit(250_000m);

        Assert.Equal(750_000m, account.Balance);
        Assert.Equal(750_000m, account.AvailableBalance);
    }

    [Fact]
    public void CashAccount_Debit_WhenBalanceIsInsufficient_ShouldReject()
    {
        var account = new CashAccount(Guid.NewGuid(), "vnd", 100_000m, 100_000m);

        Assert.Throws<InvalidOperationException>(() => account.Debit(100_001m));
    }

    [Fact]
    public void Holding_ApplyBuy_ShouldIncreaseQuantityAndWeightedAverageCost()
    {
        var holding = new Holding(Guid.NewGuid(), "hpg", "g1", 100, 100, 20_000m);

        holding.ApplyBuy(100, 30_000m);

        Assert.Equal(200, holding.Quantity);
        Assert.Equal(200, holding.AvailableQuantity);
        Assert.Equal(25_000m, holding.AverageCost);
    }

    [Fact]
    public void Holding_ApplySell_WhenQuantityIsAvailable_ShouldDecreaseQuantity()
    {
        var holding = new Holding(Guid.NewGuid(), "hpg", "g1", 100, 100, 20_000m);

        holding.ApplySell(40);

        Assert.Equal(60, holding.Quantity);
        Assert.Equal(60, holding.AvailableQuantity);
        Assert.Equal(20_000m, holding.AverageCost);
    }

    [Fact]
    public void Holding_ApplySell_WhenQuantityIsInsufficient_ShouldReject()
    {
        var holding = new Holding(Guid.NewGuid(), "hpg", "g1", 100, 100, 20_000m);

        Assert.Throws<InvalidOperationException>(() => holding.ApplySell(101));
    }

    [Fact]
    public void SimulatedOrder_Fill_ShouldCreateExecutionAndSetFilledStatus()
    {
        var order = new SimulatedOrder(Guid.NewGuid(), "hpg", "g1", OrderSide.Buy, 100, null);

        order.Fill(100, 29_150m);

        Assert.Equal(OrderStatus.Filled, order.Status);
        Assert.Equal(100, order.FilledQuantity);
        Assert.Equal(29_150m, order.AverageFillPrice);
        var execution = Assert.Single(order.Executions);
        Assert.Equal(order.Id, execution.OrderId);
        Assert.Equal(2_915_000m, execution.GrossAmount);
    }

    [Fact]
    public void SimulatedOrder_Cancel_WhenOrderIsNew_ShouldSetCancelledStatus()
    {
        var order = new SimulatedOrder(Guid.NewGuid(), "hpg", "g1", OrderSide.Buy, 100, 28_000m);

        order.Cancel();

        Assert.Equal(OrderStatus.Cancelled, order.Status);
    }

    [Fact]
    public void SimulatedOrder_Cancel_WhenOrderIsFilled_ShouldReject()
    {
        var order = new SimulatedOrder(Guid.NewGuid(), "hpg", "g1", OrderSide.Buy, 100, null);
        order.Fill(100, 29_150m);

        Assert.Throws<InvalidOperationException>(() => order.Cancel());
    }
}
