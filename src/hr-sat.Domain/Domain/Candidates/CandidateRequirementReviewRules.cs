using hr_sat.Domain;

namespace hr_sat.Domain.Candidates;

internal static class CandidateRequirementReviewRules
{
    public static Result Validate(long vacancyRequirementId) =>
        vacancyRequirementId <= 0
            ? CandidateErrors.Invalid(new Dictionary<string, string[]>
            {
                ["requirementId"] = ["Vacancy requirement is required."]
            })
            : Result.Success();
}