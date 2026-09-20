using hr_sat.Application.Abstractions.Email;
using hr_sat.Application.Abstractions.Messaging;
using hr_sat.Domain;

namespace hr_sat.Application.Features.EmailSettings.RemoveSmtpSettings;

internal sealed class RemoveSmtpSettingsCommandHandler(ISmtpSettingsStore settingsStore)
    : ICommandHandler<RemoveSmtpSettingsCommand>
{
    public async Task<Result> Handle(
        RemoveSmtpSettingsCommand command,
        CancellationToken cancellationToken)
    {
        await settingsStore.RemoveAsync(cancellationToken);
        return Result.Success();
    }
}
