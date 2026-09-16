using FluentValidation;
using hr_sat.Domain.EmailTemplates;

namespace hr_sat.Application.Features.EmailTemplates.Upsert;

public sealed class UpsertEmailTemplateCommandValidator
    : AbstractValidator<UpsertEmailTemplateCommand>
{
    public UpsertEmailTemplateCommandValidator()
    {
        RuleFor(command => command.VacancyId)
            .GreaterThan(0);
        RuleFor(command => command.Kind)
            .Must(kind => EmailTemplateKindExtensions.TryParse(kind, out _))
            .WithMessage("Kind must be shortlisted or rejected.");
        RuleFor(command => command.Subject)
            .Cascade(CascadeMode.Stop)
            .Must(subject => !string.IsNullOrWhiteSpace(subject))
            .WithMessage("Subject must contain between 1 and 998 characters after trimming.")
            .Must(subject => subject!.Trim().Length <= 998)
            .WithMessage("Subject must contain between 1 and 998 characters after trimming.");
        RuleFor(command => command.Body)
            .Must(body => !string.IsNullOrWhiteSpace(body))
            .WithMessage("Body must not be blank.");
    }
}