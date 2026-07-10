namespace InvestView.Application.Dtos.MarketData;

public sealed record SymbolMetadataDto(
    string Symbol,
    string BoardId,
    string MarketId,
    string DisplayName,
    string Name,
    string SecurityType,
    string Isin,
    string ProductGroupId,
    string SecurityGroupId,
    string TradingStatus,
    string SymbolAdminStatus,
    string TradingMethodStatus,
    string TradingSanctionStatus,
    DateTimeOffset? ListingDate,
    DateTimeOffset? FinalTradeDate,
    long OpenInterestQuantity,
    DateTimeOffset UpdatedAt);
