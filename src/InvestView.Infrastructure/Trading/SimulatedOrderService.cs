using InvestView.Application.Abstractions.MarketData;
using InvestView.Application.Abstractions.Orders;
using InvestView.Domain.Trading;
using InvestView.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace InvestView.Infrastructure.Trading;

public sealed class SimulatedOrderService : ISimulatedOrderService
{
    private const string TradingCurrency = "VND";

    private readonly InvestViewDbContext _dbContext;
    private readonly IMarketDataProvider _marketDataProvider;
    private readonly TimeProvider _timeProvider;

    public SimulatedOrderService(
        InvestViewDbContext dbContext,
        IMarketDataProvider marketDataProvider,
        TimeProvider timeProvider)
    {
        _dbContext = dbContext;
        _marketDataProvider = marketDataProvider;
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

        var detail = await _marketDataProvider.GetSymbolDetailAsync(
            normalizedSymbol,
            normalizedBoardId,
            cancellationToken);
        if (detail is null)
        {
            return new PlaceSimulatedOrderResult(PlaceSimulatedOrderStatus.SymbolNotFound, null);
        }

        var executionPrice = detail.LastPrice > 0m ? detail.LastPrice : detail.ReferencePrice;
        if (executionPrice <= 0m)
        {
            return new PlaceSimulatedOrderResult(PlaceSimulatedOrderStatus.PriceUnavailable, null);
        }

        var isMarketable = IsMarketable(command.Side, command.LimitPrice, executionPrice);
        var requiredAmount = command.Quantity * (isMarketable ? executionPrice : command.LimitPrice ?? executionPrice);
        var now = _timeProvider.GetUtcNow();
        var order = new SimulatedOrder(
            userId,
            normalizedSymbol,
            normalizedBoardId,
            command.Side,
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
            command.LimitPrice is <= 0m)
        {
            return false;
        }

        return true;
    }

    private static bool IsMarketable(
        OrderSide side,
        decimal? limitPrice,
        decimal executionPrice)
    {
        if (limitPrice is null)
        {
            return true;
        }

        return side == OrderSide.Buy
            ? limitPrice >= executionPrice
            : limitPrice <= executionPrice;
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
