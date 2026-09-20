using hr_sat.Application.Abstractions.Email;
using hr_sat.Application.Abstractions.Messaging;
using hr_sat.Domain;

namespace hr_sat.Application.Features.EmailSettings.UpsertSmtpSettings;

internal sealed class UpsertSmtpSettingsCommandHandler(ISmtpSettingsStore settingsStore)
    : ICommandHandler<UpsertSmtpSettingsCommand, UpsertSmtpSettingsResponse>
{
    public async Task<Result<UpsertSmtpSettingsResponse>> Handle(
        UpsertSmtpSettingsCommand command,
        CancellationToken cancellationToken)
    {
        // Last-write-wins (decision 30): no concurrency token, no test-before-save
        // enforcement server-side (decision 3) — the client gates Save on a passing
        // test of the current values.
        await settingsStore.SaveAsync(
            new SmtpSettingsWrite(
                command.Host!,
                command.Port!.Value,
                command.Username!,
                string.IsNullOrWhiteSpace(command.Password) ? null : command.Password,
                command.FromAddress!,
                command.FromName),
            cancellationToken);

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
            snapshot.Source.ToApiValue());
    }
}
