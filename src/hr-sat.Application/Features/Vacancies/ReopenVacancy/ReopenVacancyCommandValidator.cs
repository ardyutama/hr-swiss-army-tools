using FluentValidation;

namespace hr_sat.Application.Features.Vacancies;

public sealed class ReopenVacancyCommandValidator : AbstractValidator<ReopenVacancyCommand>
{
    public ReopenVacancyCommandValidator()
    {
        RuleFor(command => command.Id).GreaterThan(0);
    }
}
