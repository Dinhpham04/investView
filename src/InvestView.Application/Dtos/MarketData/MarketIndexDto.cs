namespace InvestView.Application.Dtos.MarketData;

public sealed record MarketIndexDto(
    string IndexName,
    decimal? Value,
    decimal? Change,
    decimal? ChangePercent,
    decimal? ReferenceValue,
    decimal? HighValue,
    decimal? LowValue,
    long? TotalVolume,
    decimal? TotalValue,
    int? UpCount,
    int? DownCount,
    int? NoChangeCount,
    int? CeilingCount,
    int? FloorCount,
    string MarketId,
    string TradingSessionId,
    DateTimeOffset UpdatedAt);
