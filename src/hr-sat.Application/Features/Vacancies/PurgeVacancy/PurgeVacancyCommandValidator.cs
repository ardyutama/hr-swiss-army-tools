using FluentValidation;

namespace hr_sat.Application.Features.Vacancies;

public sealed class PurgeVacancyCommandValidator : AbstractValidator<PurgeVacancyCommand>
{
    public PurgeVacancyCommandValidator()
    {
        RuleFor(command => command.Id).GreaterThan(0);
    }
}
