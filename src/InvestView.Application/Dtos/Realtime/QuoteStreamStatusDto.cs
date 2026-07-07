namespace InvestView.Application.Dtos.Realtime;

public sealed record QuoteStreamStatusDto(
    string Provider,
    bool IsEnabled,
    DateTimeOffset UpdatedAt,
    string Message);
