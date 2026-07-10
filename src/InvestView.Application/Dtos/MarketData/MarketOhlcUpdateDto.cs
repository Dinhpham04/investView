namespace InvestView.Application.Dtos.MarketData;

public sealed record MarketOhlcUpdateDto(
    string Symbol,
    string Resolution,
    DateTimeOffset Time,
    decimal Open,
    decimal High,
    decimal Low,
    decimal Close,
    long Volume,
    string Type,
    bool IsClosed,
    DateTimeOffset UpdatedAt);
