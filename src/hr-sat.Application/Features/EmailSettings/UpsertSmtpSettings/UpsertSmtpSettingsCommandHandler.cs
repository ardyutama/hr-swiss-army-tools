using hr_sat.Application.Abstractions.Email;
using hr_sat.Application.Abstractions.Messaging;
using hr_sat.Domain;
using hr_sat.Domain.EmailSettings;

namespace hr_sat.Application.Features.EmailSettings.UpsertSmtpSettings;

internal sealed class UpsertSmtpSettingsCommandHandler(
    ISmtpSettingsStore settingsStore,
    IMicrosoftAccountSignIn microsoftSignIn)
    : ICommandHandler<UpsertSmtpSettingsCommand, UpsertSmtpSettingsResponse>
{
    public async Task<Result<UpsertSmtpSettingsResponse>> Handle(
        UpsertSmtpSettingsCommand command,
        CancellationToken cancellationToken)
    {
        // Last-write-wins (issue 02 decision 30): no concurrency token, no
        // test-before-save enforcement server-side (decision 3) — the client gates
        // Save on a passing test of the current values. The validator ran first, so
        // the method parses.
        SignInMethodExtensions.TryParse(command.SignInMethod, out var method);

        if (method == SignInMethod.MicrosoftAccount)
        {
            // Ignore-and-derive (issue 03, decision 22): credential fields are
            // silently ignored — host, port, and username stay as the connect flow
            // derived them. Grant-existence is the method's precondition (decision
            // 30): completeness-per-method in the store means a missing or
            // unreadable grant no longer resolves as a Microsoft settings row.
            var current = await settingsStore.GetSnapshotAsync(cancellationToken);
            if (current.Source != SmtpSettingsSource.Settings ||
                current.SignInMethod != SignInMethod.MicrosoftAccount)
            {
                return Result<UpsertSmtpSettingsResponse>.Failure(
                    EmailSettingsErrors.MicrosoftSignInRequired());
            }

            await settingsStore.SaveAsync(
                new SmtpSettingsWrite.MicrosoftAccount(command.FromAddress!, command.FromName),
                cancellationToken);
        }
        else
        {
            await settingsStore.SaveAsync(
                new SmtpSettingsWrite.AppPassword(
                    command.Host!,
                    command.Port!.Value,
                    command.Username!,
                    string.IsNullOrWhiteSpace(command.Password) ? null : command.Password,
                    command.FromAddress!,
                    command.FromName),
                cancellationToken);
        }

        // The effective settings after the save — the response is the client's success
        // feedback (the status line updates in place; no toast, decision 10).
        var snapshot = await settingsStore.GetSnapshotAsync(cancellationToken);
        return new UpsertSmtpSettingsResponse(
            snapshot.Host,
            snapshot.Port,
            snapshot.Username,
            snapshot.FromAddress,
            snapshot.FromName,
            snapshot.HasPassword,
            snapshot.SignInMethod?.ToApiValue(),
            microsoftSignIn.IsAvailable,
            snapshot.Source.ToApiValue());
    }
}
