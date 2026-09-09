using FluentValidation;
using hr_sat.Domain.Candidates;

namespace hr_sat.Application.Features.Candidates.UpdateOutcome;

public sealed class UpdateCandidateOutcomeCommandValidator
    : AbstractValidator<UpdateCandidateOutcomeCommand>
{
    public UpdateCandidateOutcomeCommandValidator()
    {
        RuleFor(command => command.VacancyId).GreaterThan(0);
        RuleFor(command => command.RoundId).GreaterThan(0);
        RuleFor(command => command.CandidateId).GreaterThan(0);
        RuleFor(command => command.Outcome)
            .Must(outcome =>
                outcome is not null &&
                Enum.TryParse<CandidateHireOutcome>(outcome, true, out var parsed) &&
                Enum.IsDefined(parsed))
            .WithMessage("Hire outcome must be none, hired, runaway, or declined.");
    }
}