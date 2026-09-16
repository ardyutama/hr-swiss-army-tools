using hr_sat.Domain;
using hr_sat.Domain.Candidates;
using hr_sat.Domain.IntakeRounds;

namespace hr_sat.Domain.Vacancies;

internal static class VacancyPromotionRules
{
    public static Result<List<Candidate>> ValidatePromotion(
        VacancyStatus status,
        long vacancyId,
        long sourceRoundId,
        IntakeRound? sourceRound,
        IntakeRound? activeRound,
        IEnumerable<long>? candidateIds,
        IReadOnlyList<Candidate> roundCandidates)
    {
        if (status == VacancyStatus.Closed)
        {
            return Result<List<Candidate>>.Failure(VacancyErrors.Closed(vacancyId));
        }

        if (sourceRound is null)
        {
            return Result<List<Candidate>>.Failure(
                IntakeRoundErrors.NotFound(sourceRoundId));
        }

        if (sourceRound.IsOpen)
        {
            return Result<List<Candidate>>.Failure(
                IntakeRoundErrors.NotClosed(sourceRound.Id));
        }

        if (activeRound is null)
        {
            return Result<List<Candidate>>.Failure(IntakeRoundErrors.NoActiveRound(vacancyId));
        }

        var distinctCandidateIds = candidateIds?.Distinct().ToArray() ?? [];
        if (distinctCandidateIds.Length == 0)
        {
            return Result<List<Candidate>>.Failure(CandidateErrors.Invalid(
                new Dictionary<string, string[]>
                {
                    ["candidateIds"] = ["At least one candidate is required."]
                }));
        }

        var candidatesById = roundCandidates.ToDictionary(candidate => candidate.Id);
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
            return Result<List<Candidate>>.Failure(CandidateErrors.Invalid(
                new Dictionary<string, string[]>
                {
                    ["candidateIds"] = invalidCandidateIds.ToArray()
                }));
        }

        var duplicateSourceCandidateIds = candidates
            .Where(candidate => activeRound.Candidates.Any(existing =>
                existing.SourceSha256 is not null &&
                candidate.SourceSha256 is not null &&
                existing.SourceSha256.SequenceEqual(candidate.SourceSha256)))
            .Select(candidate => candidate.Id)
            .ToArray();
        if (duplicateSourceCandidateIds.Length > 0)
        {
            return Result<List<Candidate>>.Failure(
                CandidateErrors.SourceEmailAlreadyInRound(duplicateSourceCandidateIds));
        }

        return candidates;
    }
}
