using hr_sat.Domain;
using hr_sat.Domain.Candidates;

namespace hr_sat.Domain.IntakeRounds;

public sealed class IntakeRound : Entity
{
    private readonly List<Candidate> _candidates = [];

    private IntakeRound()
    {
    }

    private IntakeRound(int roundNumber, string? name)
    {
        RoundNumber = roundNumber;
        Name = name;
    }

    public long VacancyId { get; private set; }
    public int RoundNumber { get; private set; }
    public string? Name { get; private set; }
    public DateTimeOffset? ClosedAt { get; private set; }
    public bool IsOpen => ClosedAt is null;
    public IReadOnlyList<Candidate> Candidates => _candidates;

    internal static IntakeRound CreateDefault() => new(1, null);

    internal static Result<IntakeRound> Create(int roundNumber, string? name)
    {
        var normalizedName = string.IsNullOrWhiteSpace(name) ? null : name.Trim();
        if (normalizedName is not null && normalizedName.Length > 200)
        {
            return Result<IntakeRound>.Failure(IntakeRoundErrors.Invalid(
                new Dictionary<string, string[]>
                {
                    ["name"] = ["Name must contain 200 characters or fewer after trimming."]
                }));
        }

        return new IntakeRound(roundNumber, normalizedName);
    }

    internal Result Close(DateTimeOffset closedAt)
    {
        if (!IsOpen)
        {
            return IntakeRoundErrors.Closed(Id);
        }

        ClosedAt = closedAt;
        return Result.Success();
    }

}