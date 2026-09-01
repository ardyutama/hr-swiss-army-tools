using FluentValidation;

namespace hr_sat.Application.Features.Candidates.SelectPrimaryCv;

public sealed class SelectPrimaryCvCommandValidator : AbstractValidator<SelectPrimaryCvCommand>
{
    public SelectPrimaryCvCommandValidator()
    {
        RuleFor(command => command.VacancyId).GreaterThan(0);
        RuleFor(command => command.CandidateId).GreaterThan(0);
        RuleFor(command => command.DocumentId).GreaterThan(0);
    }
}
