using hr_sat.Application.Abstractions.Email;
using hr_sat.Application.Abstractions.Messaging;
using hr_sat.Domain;
using hr_sat.Domain.EmailSettings;

namespace hr_sat.Application.Features.EmailSettings.TestSmtpConnection;

internal sealed class TestSmtpConnectionCommandHandler(
    ISmtpSettingsStore settingsStore,
    ISmtpConnectionTester connectionTester)
    : ICommandHandler<TestSmtpConnectionCommand, TestSmtpConnectionResponse>
{
    public async Task<Result<TestSmtpConnectionResponse>> Handle(
        TestSmtpConnectionCommand command,
        CancellationToken cancellationToken)
    {
        // A blank password field means "keep the stored one", matching the upsert
        // (decision 12); with nothing stored there is nothing to authenticate with.
        var password = string.IsNullOrWhiteSpace(command.Password)
            ? null
            : command.Password.Trim();
        if (password is null &&
            !await settingsStore.HasStoredPasswordAsync(cancellationToken))
        {
            return Result<TestSmtpConnectionResponse>.Failure(EmailSettingsErrors.Invalid(
                new Dictionary<string, string[]>
                {
                    ["password"] = ["Enter the app password."]
                }));
        }

        var result = await connectionTester.TestAsync(
            new SmtpTestRequest(
                command.Host!.Trim(),
                command.Port!.Value,
                command.Username!.Trim(),
                password),
            cancellationToken);

        return result.Succeeded
            ? new TestSmtpConnectionResponse("Connected and authenticated.")
            : Result<TestSmtpConnectionResponse>.Failure(
                EmailSettingsErrors.TestFailed(result.FailureMessage!));
    }
}
