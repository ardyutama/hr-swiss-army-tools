using hr_sat.Domain;
using hr_sat.Domain.Candidates;
using hr_sat.Domain.IntakeRounds;

namespace hr_sat.Domain.Vacancies;

public sealed class Vacancy : Entity
{
    private readonly List<VacancyRequirement> _requirements = [];
    private readonly List<IntakeRound> _rounds = [];

    private Vacancy()
    {
    }

    private Vacancy(string title, DateOnly openedOn, int? neededHires)
    {
        Title = title;
        OpenedOn = openedOn;
        NeededHires = neededHires;
        Status = VacancyStatus.Open;
    }

    public string Title { get; private set; } = string.Empty;
    public DateOnly OpenedOn { get; private set; }
    public int? NeededHires { get; private set; }
    public VacancyStatus Status { get; private set; }
    public DateTimeOffset? ClosedAt { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public IReadOnlyList<VacancyRequirement> Requirements => _requirements;
    public IReadOnlyList<IntakeRound> Rounds => _rounds;
    public IntakeRound? ActiveRound => _rounds.SingleOrDefault(round => round.IsOpen);

    public Result<IntakeRound> CreateRound(string? name)
    {
        var openResult = EnsureOpen("A closed vacancy must be reopened before an intake round can be created.");
        if (openResult.IsFailure)
        {
            return Result<IntakeRound>.Failure(openResult.Error);
        }

        if (ActiveRound is not null)
        {
            return Result<IntakeRound>.Failure(IntakeRoundErrors.ActiveRoundExists(Id));
        }

        var nextRoundNumber = _rounds.Count == 0
            ? 1
            : _rounds.Max(round => round.RoundNumber) + 1;
        var roundResult = IntakeRound.Create(nextRoundNumber, name);
        if (roundResult.IsFailure)
        {
            return roundResult;
        }

        _rounds.Add(roundResult.Value);
        return roundResult.Value;
    }

    public Result CloseRound(long roundId, DateTimeOffset closedAt)
    {
        var openResult = EnsureOpen("A closed vacancy must be reopened before an intake round can be closed.");
        if (openResult.IsFailure)
        {
            return openResult;
        }

        var round = _rounds.SingleOrDefault(item => item.Id == roundId);
        return round is null
            ? IntakeRoundErrors.NotFound(roundId)
            : round.Close(closedAt);
    }

    public static Result<Vacancy> Create(
        string? title,
        DateOnly openedOn,
        IEnumerable<string?>? requirements,
        int? neededHires)
    {
        var requirementList = ValidateDefinition(title, openedOn, requirements, neededHires);
        if (requirementList.IsFailure)
        {
            return Result<Vacancy>.Failure(requirementList.Error);
        }

        var vacancy = new Vacancy(title!, openedOn, neededHires);
        vacancy.ReplaceRequirements(requirementList.Value);
        vacancy._rounds.Add(IntakeRound.CreateDefault());
        return vacancy;
    }

    public Result UpdateDefinition(
        string? title,
        DateOnly openedOn,
        IEnumerable<string?>? requirements,
        int? neededHires)
    {
        var openResult = EnsureOpen("A closed vacancy must be reopened before it can be updated.");
        if (openResult.IsFailure)
        {
            return openResult;
        }

        var requirementList = ValidateDefinition(title, openedOn, requirements, neededHires);
        if (requirementList.IsFailure)
        {
            return requirementList.Error;
        }

        Title = title!;
        OpenedOn = openedOn;
        NeededHires = neededHires;
        ReplaceRequirements(requirementList.Value);
        return Result.Success();
    }

    public Result Close(DateTimeOffset closedAt)
    {
        var openResult = EnsureOpen("A closed vacancy must be reopened before it can be closed again.");
        if (openResult.IsFailure)
        {
            return openResult;
        }

        Status = VacancyStatus.Closed;
        ClosedAt = closedAt;
        return Result.Success();
    }

    public Result Reopen()
    {
        if (Status != VacancyStatus.Closed)
        {
            return VacancyErrors.Invalid(new Dictionary<string, string[]>
            {
                ["status"] = ["Only a closed vacancy can be reopened."]
            });
        }

        Status = VacancyStatus.Open;
        ClosedAt = null;
        return Result.Success();
    }

    public Result<IntakeRound> EnsureCanReceiveCandidateImport(long roundId)
    {
        var openResult = EnsureOpen("A closed vacancy cannot receive candidate imports.");
        return openResult.IsFailure
            ? Result<IntakeRound>.Failure(openResult.Error)
            : EnsureOpenRound(roundId, requireActive: true);
    }

    public Result<IntakeRound> EnsureCanRemoveCandidate(long roundId)
    {
        var openResult = EnsureOpen(
            "A closed vacancy must be reopened before candidates can be removed.");
        return openResult.IsFailure
            ? Result<IntakeRound>.Failure(openResult.Error)
            : EnsureOpenRound(roundId, requireActive: false);
    }

    public Result<IntakeRound> EnsureCanReviewCandidate(long roundId)
    {
        var openResult = EnsureOpen(
            "A closed vacancy must be reopened before candidates can be reviewed.");
        return openResult.IsFailure
            ? Result<IntakeRound>.Failure(openResult.Error)
            : EnsureOpenRound(roundId, requireActive: false);
    }

    public Result<IntakeRound> EnsureCanRecordHireOutcome(long roundId)
    {
        if (Status == VacancyStatus.Closed)
        {
            return Result<IntakeRound>.Failure(VacancyErrors.Closed(Id));
        }

        var round = _rounds.SingleOrDefault(item => item.Id == roundId);
        return round is null
            ? Result<IntakeRound>.Failure(IntakeRoundErrors.NotFound(roundId))
            : round;
    }

    public Result<Candidate> UpdateCandidateDetails(
        long roundId,
        long candidateId,
        string? fullName,
        string? contactEmail) =>
        MutateCandidate(
            roundId,
            candidateId,
            EnsureCanReviewCandidate,
            candidate => candidate.UpdateDetails(fullName, contactEmail));

    public Result<Candidate> UpdateCandidateNotes(
        long roundId,
        long candidateId,
        string? notes) =>
        MutateCandidate(
            roundId,
            candidateId,
            EnsureCanReviewCandidate,
            candidate => candidate.UpdateNotes(notes));

    public Result<Candidate> ReviewCandidate(
        long roundId,
        long candidateId,
        CandidateReviewStatus status,
        string? notes) =>
        MutateCandidate(
            roundId,
            candidateId,
            EnsureCanReviewCandidate,
            candidate => candidate.ApplyReview(status, notes));

    public Result<Candidate> ReviewCandidateRequirement(
        long roundId,
        long candidateId,
        long vacancyRequirementId,
        bool confirmed) =>
        MutateCandidate(
            roundId,
            candidateId,
            EnsureCanReviewCandidate,
            candidate => candidate.SetRequirementReview(vacancyRequirementId, confirmed));

    public Result<Candidate> SetCandidateHireOutcome(
        long roundId,
        long candidateId,
        CandidateHireOutcome outcome,
        string? note) =>
        MutateCandidate(
            roundId,
            candidateId,
            EnsureCanRecordHireOutcome,
            candidate => candidate.SetHireOutcome(outcome, note));

    public Result<IReadOnlyList<Candidate>> PromoteCandidates(
        long sourceRoundId,
        IEnumerable<long>? candidateIds,
        DateTimeOffset promotedAt)
    {
        if (Status == VacancyStatus.Closed)
        {
            return Result<IReadOnlyList<Candidate>>.Failure(VacancyErrors.Closed(Id));
        }

        var sourceRound = _rounds.SingleOrDefault(round => round.Id == sourceRoundId);
        if (sourceRound is null)
        {
            return Result<IReadOnlyList<Candidate>>.Failure(
                IntakeRoundErrors.NotFound(sourceRoundId));
        }

        if (sourceRound.IsOpen)
        {
            return Result<IReadOnlyList<Candidate>>.Failure(
                IntakeRoundErrors.NotClosed(sourceRoundId));
        }

        var activeRound = ActiveRound;
        if (activeRound is null)
        {
            return Result<IReadOnlyList<Candidate>>.Failure(IntakeRoundErrors.NoActiveRound(Id));
        }

        var distinctCandidateIds = candidateIds?.Distinct().ToArray() ?? [];
        if (distinctCandidateIds.Length == 0)
        {
            return Result<IReadOnlyList<Candidate>>.Failure(CandidateErrors.Invalid(
                new Dictionary<string, string[]>
                {
                    ["candidateIds"] = ["At least one candidate is required."]
                }));
        }

        var candidatesById = _rounds
            .SelectMany(round => round.Candidates)
            .ToDictionary(candidate => candidate.Id);
        var candidates = new List<Candidate>(distinctCandidateIds.Length);
        var invalidCandidateIds = new List<string>();

        foreach (var candidateId in distinctCandidateIds)
        {
            if (!candidatesById.TryGetValue(candidateId, out var candidate) ||
                candidate.IntakeRoundId != sourceRound.Id)
            {
                invalidCandidateIds.Add(
                    $"Candidate with id '{candidateId}' is not in the source round.");
                continue;
            }

            if (!candidate.CanBePromoted)
            {
                invalidCandidateIds.Add(
                    $"Candidate with id '{candidateId}' is not promotable from its current state.");
                continue;
            }

            candidates.Add(candidate);
        }

        if (invalidCandidateIds.Count > 0)
        {
            return Result<IReadOnlyList<Candidate>>.Failure(CandidateErrors.Invalid(
                new Dictionary<string, string[]>
                {
                    ["candidateIds"] = invalidCandidateIds.ToArray()
                }));
        }

        var duplicateSourceCandidateIds = candidates
            .Where(candidate => activeRound.Candidates.Any(existing =>
                existing.SourceSha256.SequenceEqual(candidate.SourceSha256)))
            .Select(candidate => candidate.Id)
            .ToArray();
        if (duplicateSourceCandidateIds.Length > 0)
        {
            return Result<IReadOnlyList<Candidate>>.Failure(
                CandidateErrors.SourceEmailAlreadyInRound(duplicateSourceCandidateIds));
        }

        foreach (var candidate in candidates)
        {
            var promoteResult = candidate.PromoteTo(
                activeRound.Id,
                sourceRound.RoundNumber,
                promotedAt);
            if (promoteResult.IsFailure)
            {
                return Result<IReadOnlyList<Candidate>>.Failure(promoteResult.Error);
            }
        }

        return candidates;
    }

    private Result<Candidate> MutateCandidate(
        long roundId,
        long candidateId,
        Func<long, Result<IntakeRound>> ensureCanMutate,
        Func<Candidate, Result> mutation)
    {
        var roundResult = ensureCanMutate(roundId);
        if (roundResult.IsFailure)
        {
            return Result<Candidate>.Failure(roundResult.Error);
        }

        var candidate = roundResult.Value.Candidates
            .SingleOrDefault(item => item.Id == candidateId);
        if (candidate is null)
        {
            return Result<Candidate>.Failure(CandidateErrors.NotFound(candidateId));
        }

        var mutationResult = mutation(candidate);
        return mutationResult.IsFailure
            ? Result<Candidate>.Failure(mutationResult.Error)
            : Result<Candidate>.Success(candidate);
    }

    private Result EnsureOpen(string message)
    {
        if (Status == VacancyStatus.Closed)
        {
            return VacancyErrors.Invalid(new Dictionary<string, string[]>
            {
                ["status"] = [message]
            });
        }

        return Result.Success();
    }

    private Result<IntakeRound> EnsureOpenRound(long roundId, bool requireActive)
    {
        var round = _rounds.SingleOrDefault(item => item.Id == roundId);
        if (round is null)
        {
            return Result<IntakeRound>.Failure(IntakeRoundErrors.NotFound(roundId));
        }

        if (!round.IsOpen)
        {
            return Result<IntakeRound>.Failure(IntakeRoundErrors.Closed(round.Id));
        }

        if (requireActive && ActiveRound?.Id != round.Id)
        {
            return Result<IntakeRound>.Failure(IntakeRoundErrors.NoActiveRound(Id));
        }

        return round;
    }

    private static Result<List<string>> ValidateDefinition(
        string? title,
        DateOnly openedOn,
        IEnumerable<string?>? requirements,
        int? neededHires)
    {
        if (title is null || title.Trim().Length is < 1 or > 200)
        {
            return Result<List<string>>.Failure(VacancyErrors.Invalid(new Dictionary<string, string[]>
            {
                ["title"] = ["Title must contain between 1 and 200 characters after trimming."]
            }));
        }

        if (openedOn == default)
        {
            return Result<List<string>>.Failure(VacancyErrors.Invalid(new Dictionary<string, string[]>
            {
                ["openedOn"] = ["Opening Date is required."]
            }));
        }

        if (neededHires is < 1 or > 9999)
        {
            return Result<List<string>>.Failure(VacancyErrors.Invalid(new Dictionary<string, string[]>
            {
                ["neededHires"] = ["Needed Hires must be between 1 and 9999."]
            }));
        }

        var requirementList = requirements?.ToList() ?? [];
        if (requirementList.Count == 0)
        {
            return Result<List<string>>.Failure(VacancyErrors.Invalid(new Dictionary<string, string[]>
            {
                ["requirements"] = ["At least one vacancy requirement is required."]
            }));
        }

        var hasInvalidRequirement = requirementList.Any(requirement =>
            requirement is null || requirement.Trim().Length is < 1 or > 200);
        if (hasInvalidRequirement)
        {
            return Result<List<string>>.Failure(VacancyErrors.Invalid(new Dictionary<string, string[]>
            {
                ["requirements"] =
                    ["Each vacancy requirement must contain between 1 and 200 characters after trimming."]
            }));
        }

        var hasDuplicateRequirement = requirementList
            .GroupBy(requirement => requirement!.Trim(), StringComparer.OrdinalIgnoreCase)
            .Any(group => group.Count() > 1);
        if (hasDuplicateRequirement)
        {
            return Result<List<string>>.Failure(VacancyErrors.Invalid(new Dictionary<string, string[]>
            {
                ["requirements"] = ["Vacancy requirements must be unique after trimming and ignoring case."]
            }));
        }

        return requirementList.Select(requirement => requirement!).ToList();
    }

    private void ReplaceRequirements(IReadOnlyList<string> requirements)
    {
        _requirements.Clear();
        var position = 1;

        foreach (var requirement in requirements)
        {
            _requirements.Add(new VacancyRequirement(requirement, position));
            position++;
        }
    }
}