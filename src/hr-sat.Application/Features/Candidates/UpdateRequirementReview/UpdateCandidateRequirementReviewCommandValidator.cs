using FluentValidation;

namespace hr_sat.Application.Features.Candidates.UpdateRequirementReview;

public sealed class UpdateCandidateRequirementReviewCommandValidator
    : AbstractValidator<UpdateCandidateRequirementReviewCommand>
{
    public UpdateCandidateRequirementReviewCommandValidator()
    {
        RuleFor(command => command.VacancyId).GreaterThan(0);
        RuleFor(command => command.CandidateId).GreaterThan(0);
        RuleFor(command => command.RequirementId).GreaterThan(0);
    }
}
