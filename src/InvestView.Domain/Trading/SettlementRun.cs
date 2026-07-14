namespace InvestView.Domain.Trading;

public sealed class SettlementRun
{
    private SettlementRun()
    {
    }

    public SettlementRun(Guid? triggeredByUserId, DateTimeOffset? startedAt = null)
    {
        Id = Guid.NewGuid();
        TriggeredByUserId = triggeredByUserId;
        StartedAt = startedAt ?? DateTimeOffset.UtcNow;
        CompletedAt = StartedAt;
    }

    public Guid Id { get; private set; }

    public Guid? TriggeredByUserId { get; private set; }

    public int DueLotCount { get; private set; }

    public int SettledLotCount { get; private set; }

    public int FailedLotCount { get; private set; }

    public DateTimeOffset StartedAt { get; private set; }

    public DateTimeOffset CompletedAt { get; private set; }

    public void Complete(
        int dueLotCount,
        int settledLotCount,
        int failedLotCount,
        DateTimeOffset? completedAt = null)
    {
        if (dueLotCount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(dueLotCount), "Due lot count cannot be negative.");
        }

        if (settledLotCount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(settledLotCount), "Settled lot count cannot be negative.");
        }

        if (failedLotCount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(failedLotCount), "Failed lot count cannot be negative.");
        }

        if (settledLotCount + failedLotCount > dueLotCount)
        {
            throw new ArgumentOutOfRangeException(nameof(settledLotCount), "Settled and failed lots cannot exceed due lots.");
        }

        DueLotCount = dueLotCount;
        SettledLotCount = settledLotCount;
        FailedLotCount = failedLotCount;
        CompletedAt = completedAt ?? DateTimeOffset.UtcNow;
    }
}
