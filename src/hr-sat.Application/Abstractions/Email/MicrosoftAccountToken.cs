namespace hr_sat.Application.Abstractions.Email;

// A silently-acquired Microsoft Account token for one send (issue 03, decision 12).
// Short-lived by design — the persisted artifact is MSAL's encrypted token cache, not
// this value (decision 3).
public sealed record MicrosoftAccountToken(
    string AccessToken,
    string AccountEmail,
    DateTimeOffset ExpiresOn);
