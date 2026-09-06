using FluentValidation;

namespace hr_sat.Application.Features.Candidates.UpdateNotes;

public sealed class UpdateCandidateNotesCommandValidator
    : AbstractValidator<UpdateCandidateNotesCommand>
{
    public UpdateCandidateNotesCommandValidator()
    {
        RuleFor(command => command.VacancyId).GreaterThan(0);
        RuleFor(command => command.CandidateId).GreaterThan(0);
        RuleFor(command => command.Notes)
            .MaximumLength(4000)
            .WithMessage("Notes must be 4000 characters or fewer.");
    }
}
