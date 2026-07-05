namespace InvestView.Application.Dtos.MarketData;

public sealed record OhlcBarDto(
    string Symbol,
    string Resolution,
    DateTimeOffset Time,
    decimal Open,
    decimal High,
    decimal Low,
    decimal Close,
    long Volume);
