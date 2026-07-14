namespace InvestView.Application.Abstractions.Trading;

public interface ISettlementProcessor
{
    Task<SettlementRunDto> SettleDueLotsAsync(
        Guid? triggeredByUserId,
        CancellationToken cancellationToken);
}

public sealed record SettlementRunDto(
    Guid Id,
    DateTimeOffset StartedAt,
    DateTimeOffset CompletedAt,
    int DueLotCount,
    int SettledLotCount,
    int FailedLotCount);
