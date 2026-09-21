using hr_sat.Application.Abstractions.Email;
using hr_sat.Application.Abstractions.Messaging;
using hr_sat.Domain;

namespace hr_sat.Application.Features.EmailSettings.GetMicrosoftConnectStatus;

internal sealed class GetMicrosoftConnectStatusQueryHandler(IMicrosoftAccountSignIn microsoftSignIn)
    : IQueryHandler<GetMicrosoftConnectStatusQuery, MicrosoftConnectStatusResponse>
{
    public Task<Result<MicrosoftConnectStatusResponse>> Handle(
        GetMicrosoftConnectStatusQuery query,
        CancellationToken cancellationToken)
    {
        // Reads the in-memory attempt holder — no I/O at poll time (issue 03,
        // decision 17).
        var status = microsoftSignIn.GetConnectStatus();
        return Task.FromResult<Result<MicrosoftConnectStatusResponse>>(
            new MicrosoftConnectStatusResponse(
                status.State.ToApiValue(),
                status.AccountEmail,
                status.Error));
    }
}
