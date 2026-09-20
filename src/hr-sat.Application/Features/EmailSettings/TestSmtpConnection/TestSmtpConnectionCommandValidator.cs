using FluentValidation;
using hr_sat.Application.Features.EmailSettings.Shared;

namespace hr_sat.Application.Features.EmailSettings.TestSmtpConnection;

public sealed class TestSmtpConnectionCommandValidator : AbstractValidator<TestSmtpConnectionCommand>
{
    public TestSmtpConnectionCommandValidator()
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
        // No password rule: an omitted password legitimately means "use the stored
        // one"; the handler refuses only when no stored password exists either.
    }
}
