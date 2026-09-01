using FluentValidation;

namespace hr_sat.Application.Features.Vacancies;

public sealed class CloseVacancyCommandValidator : AbstractValidator<CloseVacancyCommand>
{
    public CloseVacancyCommandValidator()
    {
        RuleFor(command => command.Id).GreaterThan(0);
    }
}
