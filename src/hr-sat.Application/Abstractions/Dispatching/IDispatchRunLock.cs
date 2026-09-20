namespace hr_sat.Application.Abstractions.Dispatching;

// Mutual exclusion for Send To All per round (ADR-0019): only one Dispatch Run may
// execute against a round at a time. The handle is request-scoped — disposing it
// releases the lock. A null handle means another run holds the lock (fail fast).
public interface IDispatchRunLock
{
    Task<IAsyncDisposable?> TryAcquireAsync(
        long vacancyId,
        long roundId,
        CancellationToken cancellationToken);
}
