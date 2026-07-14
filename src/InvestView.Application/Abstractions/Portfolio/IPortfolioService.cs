namespace InvestView.Application.Abstractions.Portfolio;

public interface IPortfolioService
{
    Task<PortfolioSnapshotDto?> GetSnapshotAsync(
        Guid userId,
        CancellationToken cancellationToken);

    Task<PortfolioHoldingsSnapshotDto?> GetHoldingsAsync(
        Guid userId,
        CancellationToken cancellationToken);
}

public sealed record PortfolioSnapshotDto(
    IReadOnlyList<CashAccountDto> CashAccounts,
    IReadOnlyList<HoldingPositionDto> Holdings,
    decimal TotalCash,
    decimal TotalAvailableCash,
    decimal TotalMarketValue,
    decimal TotalEquity,
    decimal TotalUnrealizedPnL,
    DateTimeOffset UpdatedAt);

public sealed record CashAccountDto(
    string Currency,
    decimal Balance,
    decimal AvailableBalance,
    DateTimeOffset UpdatedAt);

public sealed record HoldingPositionDto(
    string Symbol,
    string BoardId,
    long Quantity,
    long AvailableQuantity,
    long PendingReceiveQuantity,
    decimal AverageCost,
    decimal LastPrice,
    decimal MarketValue,
    decimal CostValue,
    decimal UnrealizedPnL,
    DateTimeOffset UpdatedAt);

public sealed record PortfolioHoldingsSnapshotDto(
    IReadOnlyList<PortfolioHoldingDto> Holdings,
    long TotalQuantity,
    long TotalAvailableQuantity,
    long TotalPendingReceiveQuantity,
    decimal TotalCostValue,
    decimal TotalMarketValue,
    decimal TotalUnrealizedPnL,
    DateTimeOffset UpdatedAt);

public sealed record PortfolioHoldingDto(
    string Symbol,
    string BoardId,
    long Quantity,
    long AvailableQuantity,
    long PendingReceiveQuantity,
    long PendingT0Quantity,
    long PendingT1Quantity,
    long PendingT2Quantity,
    DateOnly? NextAvailableDate,
    decimal AverageCost,
    decimal LastPrice,
    decimal CostValue,
    decimal MarketValue,
    decimal UnrealizedPnL,
    decimal UnrealizedPnLPercent,
    DateTimeOffset UpdatedAt);
