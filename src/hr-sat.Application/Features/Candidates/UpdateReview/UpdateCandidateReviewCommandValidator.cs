using FluentValidation;
using hr_sat.Domain.Candidates;

namespace hr_sat.Application.Features.Candidates.UpdateReview;

public sealed class UpdateCandidateReviewCommandValidator
    : AbstractValidator<UpdateCandidateReviewCommand>
{
    public UpdateCandidateReviewCommandValidator()
    {
        RuleFor(command => command.VacancyId).GreaterThan(0);
        RuleFor(command => command.CandidateId).GreaterThan(0);
        RuleFor(command => command.ReviewStatus)
            .Must(status =>
                status is not null &&
                Enum.TryParse<CandidateReviewStatus>(status, true, out var parsed) &&
                parsed != CandidateReviewStatus.New)
            .WithMessage("Review status must be shortlisted, flagged, or rejected.");
        RuleFor(command => command.Notes)
            .MaximumLength(4000)
            .WithMessage("Notes must be 4000 characters or fewer.");
    }
}
