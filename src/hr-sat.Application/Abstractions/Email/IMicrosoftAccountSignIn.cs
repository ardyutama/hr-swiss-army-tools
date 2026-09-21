namespace hr_sat.Application.Abstractions.Email;

// The Microsoft Account sign-in boundary (issue 03, decisions 3, 17): MSAL.NET lives
// in Infrastructure behind this interface so tests substitute it — no live Microsoft
// calls in the suite. One attempt is in flight per process; a new BeginConnectAsync
// replaces it (last-write-wins, issue 02 decision 30).
public interface IMicrosoftAccountSignIn
{
    // Whether Microsoft sign-in is offered at all: the installation's
    // Smtp:MicrosoftClientId key is present (decision 24). The settings GET surfaces
    // it so the client can disable the choice before the click; begin guards
    // server-side against the same condition.
    bool IsAvailable { get; }

    // Starts a device-code connect attempt and returns the challenge to show the user.
    // The flow completes out of band; poll GetConnectStatus for the outcome.
    Task<MicrosoftConnectChallenge> BeginConnectAsync(CancellationToken cancellationToken);

    // The current attempt's state, read from the in-memory attempt holder — no I/O at
    // poll time (decision 17). A process restart mid-connect means no attempt: Idle.
    MicrosoftConnectStatus GetConnectStatus();

    // Silent token acquisition for the Microsoft Account send path: the MSAL cache
    // answers from memory, hitting the network only near expiry (decision 12).
    // Throws when the grant is dead (the caller maps that to SignInExpired).
    Task<MicrosoftAccountToken> AcquireTokenAsync(CancellationToken cancellationToken);
}
