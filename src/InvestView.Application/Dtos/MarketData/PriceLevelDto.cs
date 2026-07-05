namespace InvestView.Application.Dtos.MarketData;

public sealed record PriceLevelDto(
    decimal Price,
    long Quantity);
