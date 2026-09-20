using hr_sat.Application.Abstractions.Email;
using hr_sat.Application.Abstractions.Messaging;
using hr_sat.Domain;

namespace hr_sat.Application.Features.EmailSettings.GetSmtpSettings;

internal sealed class GetSmtpSettingsQueryHandler(ISmtpSettingsStore settingsStore)
    : IQueryHandler<GetSmtpSettingsQuery, SmtpSettingsResponse>
{
    public async Task<Result<SmtpSettingsResponse>> Handle(
        GetSmtpSettingsQuery query,
        CancellationToken cancellationToken)
    {
        var snapshot = await settingsStore.GetSnapshotAsync(cancellationToken);
        return new SmtpSettingsResponse(
            snapshot.Host,
            snapshot.Port,
            snapshot.Username,
            snapshot.FromAddress,
            snapshot.FromName,
            snapshot.HasPassword,
            snapshot.Source.ToApiValue());
    }
}
