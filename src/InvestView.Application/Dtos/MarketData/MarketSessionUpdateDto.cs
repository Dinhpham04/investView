namespace InvestView.Application.Dtos.MarketData;

public sealed record MarketSessionUpdateDto(
    string MarketId,
    string BoardId,
    string ProductGroupId,
    string EventId,
    string TradingSessionId,
    DateTimeOffset UpdatedAt);
