namespace hr_sat.Application.Features.EmailSettings.GetMicrosoftConnectStatus;

// The connect attempt's state as the wire carries it (issue 03, decision 21):
// 'idle' | 'pending' | 'succeeded' | 'failed' | 'expired'; accountEmail on
// succeeded, error on failed.
public sealed record MicrosoftConnectStatusResponse(
    string State,
    string? AccountEmail,
    string? Error);
