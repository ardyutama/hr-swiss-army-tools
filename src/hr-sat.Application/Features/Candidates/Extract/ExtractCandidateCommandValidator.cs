using FluentValidation;

namespace hr_sat.Application.Features.Candidates.Extract;

public sealed class ExtractCandidateCommandValidator : AbstractValidator<ExtractCandidateCommand>
{
    public ExtractCandidateCommandValidator()
    {
        RuleFor(command => command.VacancyId).GreaterThan(0);
        RuleFor(command => command.CandidateId).GreaterThan(0);
    }
}
