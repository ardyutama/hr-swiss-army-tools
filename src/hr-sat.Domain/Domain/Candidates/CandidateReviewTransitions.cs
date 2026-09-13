using hr_sat.Domain;

namespace hr_sat.Domain.Candidates;

internal static class CandidateReviewTransitions
{
    public static Result Validate(CandidateReviewStatus status) =>
        status == CandidateReviewStatus.New
            ? CandidateErrors.Invalid(new Dictionary<string, string[]>
            {
                ["reviewStatus"] = ["A review decision must be shortlisted, flagged, or rejected."]
            })
            : Result.Success();
}