namespace hr_sat.Application.Features.EmailSettings.BeginMicrosoftConnect;

// The device-code challenge for the settings page's connect panel (issue 03,
// decision 32): the code to type at the verification URL, and when it expires.
public sealed record MicrosoftConnectChallengeResponse(
    string UserCode,
    string VerificationUrl,
    DateTimeOffset ExpiresAt);
