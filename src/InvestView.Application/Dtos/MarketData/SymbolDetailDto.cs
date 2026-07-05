namespace InvestView.Application.Dtos.MarketData;

public sealed record SymbolDetailDto(
    string Symbol,
    string BoardId,
    string MarketId,
    string DisplayName,
    string Name,
    string SecurityType,
    decimal ReferencePrice,
    decimal CeilingPrice,
    decimal FloorPrice,
    string TradingStatus,
    DateTimeOffset UpdatedAt);
