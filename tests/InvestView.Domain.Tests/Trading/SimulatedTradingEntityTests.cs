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
    public void CashAccount_Reserve_WhenBalanceIsAvailable_ShouldDecreaseAvailableBalanceOnly()
    {
        var account = new CashAccount(Guid.NewGuid(), "vnd", 1_000_000m, 1_000_000m);

        account.Reserve(250_000m);

        Assert.Equal(1_000_000m, account.Balance);
        Assert.Equal(750_000m, account.AvailableBalance);
    }

    [Fact]
    public void CashAccount_ReleaseReservation_ShouldRestoreAvailableBalance()
    {
        var account = new CashAccount(Guid.NewGuid(), "vnd", 1_000_000m, 1_000_000m);
        account.Reserve(250_000m);

        account.ReleaseReservation(250_000m);

        Assert.Equal(1_000_000m, account.Balance);
        Assert.Equal(1_000_000m, account.AvailableBalance);
    }

    [Fact]
    public void Holding_ApplyBuy_ShouldIncreaseQuantityPendingReceiveAndWeightedAverageCost()
    {
        var holding = new Holding(Guid.NewGuid(), "hpg", "g1", 100, 100, 20_000m);

        holding.ApplyBuy(100, 30_000m);

        Assert.Equal(200, holding.Quantity);
        Assert.Equal(100, holding.AvailableQuantity);
        Assert.Equal(100, holding.PendingReceiveQuantity);
        Assert.Equal(25_000m, holding.AverageCost);
    }

    [Fact]
    public void Holding_SettleReceivedQuantity_ShouldMovePendingReceiveToAvailable()
    {
        var holding = new Holding(Guid.NewGuid(), "hpg", "g1", 0, 0, 0m);
        holding.ApplyBuy(100, 30_000m);

        holding.SettleReceivedQuantity(60);

        Assert.Equal(100, holding.Quantity);
        Assert.Equal(60, holding.AvailableQuantity);
        Assert.Equal(40, holding.PendingReceiveQuantity);
    }

    [Fact]
    public void HoldingSettlementLot_WhenCreated_ShouldNormalizeIdentityAndRemainPending()
    {
        var userId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var executionId = Guid.NewGuid();

        var lot = new HoldingSettlementLot(
            userId,
            " hpg ",
            " g1 ",
            orderId,
            executionId,
            100,
            new DateOnly(2026, 7, 14),
            new DateOnly(2026, 7, 16),
            new DateOnly(2026, 7, 16));

        Assert.Equal(userId, lot.UserId);
        Assert.Equal("HPG", lot.Symbol);
        Assert.Equal("G1", lot.BoardId);
        Assert.Equal(orderId, lot.SourceOrderId);
        Assert.Equal(executionId, lot.SourceExecutionId);
        Assert.Equal(100, lot.Quantity);
        Assert.Equal(100, lot.RemainingQuantity);
        Assert.Equal(HoldingSettlementLotStatus.Pending, lot.Status);
    }

    [Fact]
    public void HoldingSettlementLot_MarkSettled_ShouldClearRemainingQuantity()
    {
        var lot = new HoldingSettlementLot(
            Guid.NewGuid(),
            "HPG",
            "G1",
            Guid.NewGuid(),
            Guid.NewGuid(),
            100,
            new DateOnly(2026, 7, 14),
            new DateOnly(2026, 7, 16),
            new DateOnly(2026, 7, 16));

        lot.MarkSettled(new DateTimeOffset(2026, 7, 16, 2, 0, 0, TimeSpan.Zero));

        Assert.Equal(0, lot.RemainingQuantity);
        Assert.Equal(HoldingSettlementLotStatus.Settled, lot.Status);
        Assert.NotNull(lot.SettledAt);
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
    public void Holding_ReserveSell_WhenQuantityIsAvailable_ShouldDecreaseAvailableQuantityOnly()
    {
        var holding = new Holding(Guid.NewGuid(), "hpg", "g1", 200, 100, 20_000m);
        holding.ApplyBuy(100, 30_000m);

        holding.ReserveSell(40);

        Assert.Equal(300, holding.Quantity);
        Assert.Equal(60, holding.AvailableQuantity);
        Assert.Equal(100, holding.PendingReceiveQuantity);
    }

    [Fact]
    public void Holding_ReleaseSellReservation_ShouldRestoreAvailableQuantity()
    {
        var holding = new Holding(Guid.NewGuid(), "hpg", "g1", 100, 100, 20_000m);
        holding.ReserveSell(40);

        holding.ReleaseSellReservation(40);

        Assert.Equal(100, holding.Quantity);
        Assert.Equal(100, holding.AvailableQuantity);
    }

    [Fact]
    public void SimulatedOrder_Fill_ShouldCreateExecutionAndSetFilledStatus()
    {
        var order = new SimulatedOrder(Guid.NewGuid(), "hpg", "g1", OrderSide.Buy, OrderType.MTL, 100, null);

        order.Fill(100, 29_150m);

        Assert.Equal(OrderType.MTL, order.OrderType);
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
        var order = new SimulatedOrder(Guid.NewGuid(), "hpg", "g1", OrderSide.Buy, OrderType.LO, 100, 28_000m);

        order.Cancel();

        Assert.Equal(OrderStatus.Cancelled, order.Status);
    }

    [Fact]
    public void SimulatedOrder_Cancel_WhenOrderIsFilled_ShouldReject()
    {
        var order = new SimulatedOrder(Guid.NewGuid(), "hpg", "g1", OrderSide.Buy, OrderType.MTL, 100, null);
        order.Fill(100, 29_150m);

        Assert.Throws<InvalidOperationException>(() => order.Cancel());
    }
}
