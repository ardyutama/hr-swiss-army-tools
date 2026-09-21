using FluentValidation;
using hr_sat.Application.Abstractions.Email;
using hr_sat.Application.Features.EmailSettings.Shared;

namespace hr_sat.Application.Features.EmailSettings.UpsertSmtpSettings;

// Method-conditional input rules (issue 03, decision 30), mirroring the client's
// validation.ts: App Password validates the full credential set (issue 02, unchanged);
// Microsoft Account validates only the From fields — the credential columns are
// derived from the connected account (decision 22). The grant-existence rule is the
// handler's: validators stay sync and pure (ADR-0004).
public sealed class UpsertSmtpSettingsCommandValidator : AbstractValidator<UpsertSmtpSettingsCommand>
{
    public UpsertSmtpSettingsCommandValidator()
    {
        RuleFor(command => command.SignInMethod)
            .Must(method => SignInMethodExtensions.TryParse(method, out _))
            .WithMessage("Sign-in method must be app-password or microsoft-account.");

        When(
            command => MethodOf(command) == SignInMethod.AppPassword,
            () =>
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
            });

        // The From fields are the only values either method trusts from the wire.
        When(
            command => MethodOf(command) is not null,
            () =>
            {
                RuleFor(command => command.FromAddress)
                    .Must(SmtpSettingsValidation.IsValidEmailAddress)
                    .WithMessage("Enter a valid email address.");
                RuleFor(command => command.FromName)
                    .Must(name => name is null ||
                        name.Trim().Length <= SmtpSettingsValidation.FromNameMaxLength)
                    .WithMessage($"From name must be {SmtpSettingsValidation.FromNameMaxLength} characters or fewer.");
            });
    }

    // The validator ran before the handler (ValidationDecorator), so an unparseable
    // method never reaches the branches that consult it.
    private static SignInMethod? MethodOf(UpsertSmtpSettingsCommand command) =>
        SignInMethodExtensions.TryParse(command.SignInMethod, out var method) ? method : null;
}
