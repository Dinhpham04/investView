namespace InvestView.Application.Abstractions.Trading;

public interface ISettlementDateCalculator
{
    SettlementDates CalculateStockSettlement(string boardId, DateTimeOffset executionTime);
}

public sealed record SettlementDates(
    DateOnly TradeDate,
    DateOnly SettlementDate,
    DateOnly AvailableFromDate);

