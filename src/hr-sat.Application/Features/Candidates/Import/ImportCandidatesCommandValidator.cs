using FluentValidation;

namespace hr_sat.Application.Features.Candidates.Import;

public sealed class ImportCandidatesCommandValidator : AbstractValidator<ImportCandidatesCommand>
{
    public ImportCandidatesCommandValidator()
    {
        RuleFor(command => command.VacancyId).GreaterThan(0);
        RuleFor(command => command.Files)
            .NotEmpty()
            .WithMessage("At least one .eml file is required.");
    }
}
