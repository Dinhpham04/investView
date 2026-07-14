using InvestView.Application.Abstractions.MarketData;
using InvestView.Application.Abstractions.Orders;
using InvestView.Application.Abstractions.Trading;
using InvestView.Application.Dtos.MarketData;
using InvestView.Domain.Trading;
using InvestView.Infrastructure.Data;
using InvestView.Infrastructure.MarketData;
using Microsoft.EntityFrameworkCore;

namespace InvestView.Infrastructure.Trading;

public sealed class SimulatedOrderService : ISimulatedOrderService
{
    private const string StockProductGroupId = "STO";
    private const string TradingCurrency = "VND";

    private readonly InvestViewDbContext _dbContext;
    private readonly IMarketDataProvider _marketDataProvider;
    private readonly IMarketStateStore _marketStateStore;
    private readonly ISettlementDateCalculator _settlementDateCalculator;
    private readonly TimeProvider _timeProvider;

    public SimulatedOrderService(
        InvestViewDbContext dbContext,
        IMarketDataProvider marketDataProvider,
        IMarketStateStore marketStateStore,
        ISettlementDateCalculator settlementDateCalculator,
        TimeProvider timeProvider)
    {
        _dbContext = dbContext;
        _marketDataProvider = marketDataProvider;
        _marketStateStore = marketStateStore;
        _settlementDateCalculator = settlementDateCalculator;
        _timeProvider = timeProvider;
    }

    public async Task<IReadOnlyList<SimulatedOrderDto>> ListAsync(
        Guid userId,
        CancellationToken cancellationToken)
    {
        if (userId == Guid.Empty)
        {
            return [];
        }

        var orders = await _dbContext.Orders
            .AsNoTracking()
            .Include(order => order.Executions)
            .Where(order => order.UserId == userId)
            .OrderByDescending(order => order.CreatedAt)
            .ToArrayAsync(cancellationToken);

        return orders.Select(ToDto).ToArray();
    }

    public async Task<PlaceSimulatedOrderResult> PlaceAsync(
        Guid userId,
        PlaceSimulatedOrderCommand command,
        CancellationToken cancellationToken)
    {
        if (!TryValidate(command, out var normalizedSymbol, out var normalizedBoardId))
        {
            return new PlaceSimulatedOrderResult(PlaceSimulatedOrderStatus.InvalidInput, null);
        }

        if (userId == Guid.Empty ||
            !await _dbContext.Users.AnyAsync(user => user.Id == userId, cancellationToken))
        {
            return new PlaceSimulatedOrderResult(PlaceSimulatedOrderStatus.UserNotFound, null);
        }

        var now = _timeProvider.GetUtcNow();
        if (!await CanPlaceSimulatedOrderAsync(normalizedBoardId, now, cancellationToken))
        {
            return new PlaceSimulatedOrderResult(PlaceSimulatedOrderStatus.MarketClosed, null);
        }

        var detail = await _marketDataProvider.GetSymbolDetailAsync(
            normalizedSymbol,
            normalizedBoardId,
            cancellationToken);
        if (detail is null)
        {
            return new PlaceSimulatedOrderResult(PlaceSimulatedOrderStatus.SymbolNotFound, null);
        }

        var marketPrice = detail.LastPrice > 0m ? detail.LastPrice : detail.ReferencePrice;
        if (marketPrice <= 0m)
        {
            return new PlaceSimulatedOrderResult(PlaceSimulatedOrderStatus.PriceUnavailable, null);
        }

        var isMarketable = IsMarketable(command.Side, command.OrderType, command.LimitPrice, marketPrice);
        var requiredAmount = command.Quantity * GetRequiredCashPrice(command.OrderType, command.LimitPrice, marketPrice);
        var executionPrice = marketPrice;
        var order = new SimulatedOrder(
            userId,
            normalizedSymbol,
            normalizedBoardId,
            command.Side,
            command.OrderType,
            command.Quantity,
            command.LimitPrice,
            now);

        if (command.Side == OrderSide.Buy)
        {
            var cashAccount = await FindCashAccountAsync(userId, cancellationToken);
            if (cashAccount is null || cashAccount.AvailableBalance < requiredAmount)
            {
                return new PlaceSimulatedOrderResult(PlaceSimulatedOrderStatus.InsufficientCash, null);
            }

            if (isMarketable)
            {
                cashAccount.Debit(command.Quantity * executionPrice, now);
                var holding = await GetOrCreateHoldingAsync(
                    userId,
                    normalizedSymbol,
                    normalizedBoardId,
                    now,
                    cancellationToken);
                holding.ApplyBuy(command.Quantity, executionPrice, now);
                order.Fill(command.Quantity, executionPrice, now);
                var execution = order.Executions.Single();
                var settlementDates = _settlementDateCalculator.CalculateStockSettlement(normalizedBoardId, execution.ExecutedAt);
                _dbContext.HoldingSettlementLots.Add(new HoldingSettlementLot(
                    userId,
                    normalizedSymbol,
                    normalizedBoardId,
                    order.Id,
                    execution.Id,
                    execution.Quantity,
                    settlementDates.TradeDate,
                    settlementDates.SettlementDate,
                    settlementDates.AvailableFromDate,
                    now));
            }
            else
            {
                cashAccount.Reserve(requiredAmount, now);
            }
        }
        else
        {
            var holding = await FindHoldingAsync(
                userId,
                normalizedSymbol,
                normalizedBoardId,
                cancellationToken);
            if (holding is null || holding.AvailableQuantity < command.Quantity)
            {
                return new PlaceSimulatedOrderResult(PlaceSimulatedOrderStatus.InsufficientHolding, null);
            }

            if (isMarketable)
            {
                holding.ApplySell(command.Quantity, now);
                var cashAccount = await GetOrCreateCashAccountAsync(userId, now, cancellationToken);
                cashAccount.Credit(command.Quantity * executionPrice, now);
                order.Fill(command.Quantity, executionPrice, now);
            }
            else
            {
                holding.ReserveSell(command.Quantity, now);
            }
        }

        _dbContext.Orders.Add(order);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return new PlaceSimulatedOrderResult(PlaceSimulatedOrderStatus.Created, ToDto(order));
    }

    public async Task<CancelSimulatedOrderResult> CancelAsync(
        Guid userId,
        Guid orderId,
        CancellationToken cancellationToken)
    {
        if (userId == Guid.Empty || orderId == Guid.Empty)
        {
            return new CancelSimulatedOrderResult(CancelSimulatedOrderStatus.NotFound, null);
        }

        var order = await _dbContext.Orders
            .Include(candidate => candidate.Executions)
            .SingleOrDefaultAsync(
                candidate => candidate.UserId == userId && candidate.Id == orderId,
                cancellationToken);
        if (order is null)
        {
            return new CancelSimulatedOrderResult(CancelSimulatedOrderStatus.NotFound, null);
        }

        try
        {
            await ReleaseReservedAssetsAsync(order, cancellationToken);
            order.Cancel(_timeProvider.GetUtcNow());
        }
        catch (InvalidOperationException)
        {
            return new CancelSimulatedOrderResult(CancelSimulatedOrderStatus.CannotCancel, ToDto(order));
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        return new CancelSimulatedOrderResult(CancelSimulatedOrderStatus.Cancelled, ToDto(order));
    }

    private static bool TryValidate(
        PlaceSimulatedOrderCommand command,
        out string normalizedSymbol,
        out string normalizedBoardId)
    {
        try
        {
            normalizedSymbol = MarketIdentity.NormalizeSymbol(command.Symbol);
            normalizedBoardId = MarketIdentity.NormalizeBoardId(command.BoardId);
        }
        catch (ArgumentException)
        {
            normalizedSymbol = string.Empty;
            normalizedBoardId = string.Empty;
            return false;
        }

        if (!Enum.IsDefined(command.Side) ||
            command.Quantity <= 0 ||
            !Enum.IsDefined(command.OrderType) ||
            command.OrderType == OrderType.LO && (command.LimitPrice is null or <= 0m) ||
            command.OrderType != OrderType.LO && command.LimitPrice is not null ||
            command.OrderType is OrderType.ATO or OrderType.ATC)
        {
            return false;
        }

        return true;
    }

    private async Task<bool> CanPlaceSimulatedOrderAsync(
        string boardId,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var cachedSession = await _marketStateStore.GetMarketSessionAsync(
            StockProductGroupId,
            boardId,
            cancellationToken);
        var session = cachedSession is null
            ? MarketSessionResolver.Resolve(CreateFallbackSession(boardId, now), now)
            : MarketSessionResolver.Resolve(cachedSession, now);

        return session.IsOpen && session.IsContinuous;
    }

    private static MarketSessionUpdateDto CreateFallbackSession(string boardId, DateTimeOffset now)
    {
        return new MarketSessionUpdateDto(
            MarketId: "VN",
            BoardId: boardId,
            ProductGroupId: StockProductGroupId,
            EventId: string.Empty,
            TradingSessionId: string.Empty,
            UpdatedAt: now);
    }

    private static bool IsMarketable(
        OrderSide side,
        OrderType orderType,
        decimal? limitPrice,
        decimal executionPrice)
    {
        if (orderType == OrderType.MTL)
        {
            return true;
        }

        if (orderType != OrderType.LO)
        {
            return false;
        }

        if (limitPrice is null)
        {
            return false;
        }

        return side == OrderSide.Buy
            ? limitPrice >= executionPrice
            : limitPrice <= executionPrice;
    }

    private static decimal GetRequiredCashPrice(
        OrderType orderType,
        decimal? limitPrice,
        decimal marketPrice)
    {
        return orderType == OrderType.LO
            ? limitPrice ?? marketPrice
            : marketPrice;
    }

    private async Task ReleaseReservedAssetsAsync(
        SimulatedOrder order,
        CancellationToken cancellationToken)
    {
        if (order.Status != OrderStatus.New)
        {
            return;
        }

        var now = _timeProvider.GetUtcNow();
        if (order.Side == OrderSide.Buy)
        {
            if (order.LimitPrice is null)
            {
                return;
            }

            var cashAccount = await FindCashAccountAsync(order.UserId, cancellationToken);
            cashAccount?.ReleaseReservation(order.Quantity * order.LimitPrice.Value, now);
            return;
        }

        var holding = await FindHoldingAsync(
            order.UserId,
            order.Symbol,
            order.BoardId,
            cancellationToken);
        holding?.ReleaseSellReservation(order.Quantity, now);
    }

    private Task<CashAccount?> FindCashAccountAsync(
        Guid userId,
        CancellationToken cancellationToken)
    {
        return _dbContext.CashAccounts.SingleOrDefaultAsync(
            account => account.UserId == userId && account.Currency == TradingCurrency,
            cancellationToken);
    }

    private async Task<CashAccount> GetOrCreateCashAccountAsync(
        Guid userId,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var cashAccount = await FindCashAccountAsync(userId, cancellationToken);
        if (cashAccount is not null)
        {
            return cashAccount;
        }

        cashAccount = new CashAccount(userId, TradingCurrency, 0m, 0m, now);
        _dbContext.CashAccounts.Add(cashAccount);
        return cashAccount;
    }

    private Task<Holding?> FindHoldingAsync(
        Guid userId,
        string symbol,
        string boardId,
        CancellationToken cancellationToken)
    {
        return _dbContext.Holdings.SingleOrDefaultAsync(
            holding =>
                holding.UserId == userId &&
                holding.Symbol == symbol &&
                holding.BoardId == boardId,
            cancellationToken);
    }

    private async Task<Holding> GetOrCreateHoldingAsync(
        Guid userId,
        string symbol,
        string boardId,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var holding = await FindHoldingAsync(userId, symbol, boardId, cancellationToken);
        if (holding is not null)
        {
            return holding;
        }

        holding = new Holding(userId, symbol, boardId, 0, 0, 0m, now);
        _dbContext.Holdings.Add(holding);
        return holding;
    }

    private static SimulatedOrderDto ToDto(SimulatedOrder order)
    {
        return new SimulatedOrderDto(
            order.Id,
            order.Symbol,
            order.BoardId,
            order.Side,
            order.OrderType,
            order.Quantity,
            order.LimitPrice,
            order.Status,
            order.FilledQuantity,
            order.AverageFillPrice,
            order.CreatedAt,
            order.UpdatedAt,
            order.Executions
                .OrderBy(execution => execution.ExecutedAt)
                .Select(execution => new OrderExecutionDto(
                    execution.Id,
                    execution.Quantity,
                    execution.Price,
                    execution.GrossAmount,
                    execution.ExecutedAt))
                .ToArray());
    }
}
