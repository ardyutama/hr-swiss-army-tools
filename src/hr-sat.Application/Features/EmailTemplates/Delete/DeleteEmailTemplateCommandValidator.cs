using FluentValidation;
using hr_sat.Domain.EmailTemplates;

namespace hr_sat.Application.Features.EmailTemplates.Delete;

public sealed class DeleteEmailTemplateCommandValidator
    : AbstractValidator<DeleteEmailTemplateCommand>
{
    public DeleteEmailTemplateCommandValidator()
    {
        RuleFor(command => command.VacancyId)
            .GreaterThan(0);
        RuleFor(command => command.Kind)
            .Must(kind => EmailTemplateKindExtensions.TryParse(kind, out _))
            .WithMessage("Kind must be shortlisted or rejected.");
    }
}