using FluentValidation;
using hr_sat.Application.Features.EmailSettings.Shared;

namespace hr_sat.Application.Features.EmailSettings.UpsertSmtpSettings;

public sealed class UpsertSmtpSettingsCommandValidator : AbstractValidator<UpsertSmtpSettingsCommand>
{
    public UpsertSmtpSettingsCommandValidator()
    {
        RuleFor(command => command.Host)
            .Must(SmtpSettingsValidation.IsValidHost)
            .WithMessage("Enter a hostname or IP address.");
        RuleFor(command => command.Port)
            .Must(port => SmtpSettingsValidation.IsValidPort(port))
            .WithMessage("Enter a port between 1 and 65535.");
        RuleFor(command => command.Username)
            .Must(username => !string.IsNullOrWhiteSpace(username))
            .WithMessage("Enter the account username.");
        RuleFor(command => command.Password)
            .Must(password => password is null || password.Trim().Length > 0)
            .WithMessage("Enter the app password.");
        RuleFor(command => command.FromAddress)
            .Must(SmtpSettingsValidation.IsValidEmailAddress)
            .WithMessage("Enter a valid email address.");
        RuleFor(command => command.FromName)
            .Must(name => name is null ||
                name.Trim().Length <= SmtpSettingsValidation.FromNameMaxLength)
            .WithMessage($"From name must be {SmtpSettingsValidation.FromNameMaxLength} characters or fewer.");
    }
}
