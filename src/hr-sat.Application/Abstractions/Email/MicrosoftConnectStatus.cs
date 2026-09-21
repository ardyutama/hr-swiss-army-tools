namespace hr_sat.Application.Abstractions.Email;

// The connect attempt's outcome as the status poll reports it (issue 03, decisions
// 17, 21). AccountEmail is set on Succeeded — the derived sender identity (decision
// 5); Error carries sanitized copy on Failed, never raw MSAL text.
public sealed record MicrosoftConnectStatus(
    MicrosoftConnectState State,
    string? AccountEmail,
    string? Error);
