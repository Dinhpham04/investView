namespace InvestView.Application.Dtos.MarketData;

public sealed record MarketTradeUpdateDto(
    string Symbol,
    string BoardId,
    DateTimeOffset Time,
    decimal? Price,
    decimal? Change,
    decimal? ChangePercent,
    long? Quantity,
    long? TotalVolume,
    decimal? TotalValue,
    string Side);
