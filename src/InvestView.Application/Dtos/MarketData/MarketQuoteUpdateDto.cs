namespace InvestView.Application.Dtos.MarketData;

public sealed record MarketQuoteUpdateDto(
    string Symbol,
    string BoardId,
    decimal? LastPrice,
    decimal? Change,
    decimal? ChangePercent,
    long? LastQuantity,
    long? TotalVolume,
    decimal? TotalValue,
    IReadOnlyList<PriceLevelDto>? BidLevels,
    IReadOnlyList<PriceLevelDto>? AskLevels,
    string? TradingStatus,
    DateTimeOffset UpdatedAt);
