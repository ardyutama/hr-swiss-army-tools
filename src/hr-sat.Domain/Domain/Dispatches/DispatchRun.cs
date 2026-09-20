using hr_sat.Domain;

namespace hr_sat.Domain.Dispatches;

public sealed class DispatchRun : Entity
{
    private DispatchRun()
    {
    }

    private DispatchRun(long vacancyId, long intakeRoundId, DateTimeOffset startedAt)
    {
        VacancyId = vacancyId;
        IntakeRoundId = intakeRoundId;
        StartedAt = startedAt;
    }

    public long VacancyId { get; private set; }
    public long IntakeRoundId { get; private set; }
    public DateTimeOffset StartedAt { get; private set; }
    public DateTimeOffset? CompletedAt { get; private set; }

    internal static DispatchRun Start(
        long vacancyId,
        long intakeRoundId,
        DateTimeOffset startedAt) =>
        new(vacancyId, intakeRoundId, startedAt);

    internal void Complete(DateTimeOffset completedAt)
    {
        CompletedAt ??= completedAt;
    }
}
