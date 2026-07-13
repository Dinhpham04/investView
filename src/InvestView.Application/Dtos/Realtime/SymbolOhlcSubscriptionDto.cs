namespace InvestView.Application.Dtos.Realtime;

public sealed record SymbolOhlcSubscriptionDto(
    string? Symbol,
    IReadOnlyCollection<string>? Resolutions);
