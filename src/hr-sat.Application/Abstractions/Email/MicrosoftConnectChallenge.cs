namespace hr_sat.Application.Abstractions.Email;

// The device-code challenge a connect attempt shows the user (issue 03, decision 10):
// the code to type at the verification URL, and when it expires.
public sealed record MicrosoftConnectChallenge(
    string UserCode,
    string VerificationUrl,
    DateTimeOffset ExpiresAt);
