using hr_sat.Application.Abstractions.Email;
using hr_sat.Application.Abstractions.Messaging;
using hr_sat.Domain;
using hr_sat.Domain.EmailSettings;

namespace hr_sat.Application.Features.EmailSettings.BeginMicrosoftConnect;

internal sealed class BeginMicrosoftConnectCommandHandler(IMicrosoftAccountSignIn microsoftSignIn)
    : ICommandHandler<BeginMicrosoftConnectCommand, MicrosoftConnectChallengeResponse>
{
    public async Task<Result<MicrosoftConnectChallengeResponse>> Handle(
        BeginMicrosoftConnectCommand command,
        CancellationToken cancellationToken)
    {
        // Server-side guard for a hand-rolled begin (issue 03, decision 24): the client
        // disables the choice from the GET's microsoftSignInAvailable before the click.
        if (!microsoftSignIn.IsAvailable)
        {
            return Result<MicrosoftConnectChallengeResponse>.Failure(
                EmailSettingsErrors.MicrosoftSignInUnavailable());
        }

        var challenge = await microsoftSignIn.BeginConnectAsync(cancellationToken);
        return new MicrosoftConnectChallengeResponse(
            challenge.UserCode,
            challenge.VerificationUrl,
            challenge.ExpiresAt);
    }
}
