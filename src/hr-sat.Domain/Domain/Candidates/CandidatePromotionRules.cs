using hr_sat.Domain;

namespace hr_sat.Domain.Candidates;

internal static class CandidatePromotionRules
{
    public static bool CanBePromoted(
        CandidateReviewStatus reviewStatus,
        CandidateHireOutcome hireOutcome) =>
        reviewStatus is (CandidateReviewStatus.New or CandidateReviewStatus.Flagged or CandidateReviewStatus.Shortlisted) &&
        hireOutcome == CandidateHireOutcome.None;

    public static Result ValidateDetails(
        long targetRoundId,
        int sourceRoundNumber,
        DateTimeOffset promotedAt) =>
        targetRoundId <= 0 || sourceRoundNumber <= 0 || promotedAt == default
            ? CandidateErrors.Invalid(new Dictionary<string, string[]>
            {
                ["promotion"] = ["Promotion details are invalid."]
            })
            : Result.Success();
}