namespace InvestView.Application.Dtos.Realtime;

public sealed record MarketBoardSubscriptionDto(
    string? BoardId,
    IReadOnlyCollection<string>? Symbols);
