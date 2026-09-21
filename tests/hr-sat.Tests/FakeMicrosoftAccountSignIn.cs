using hr_sat.Application.Abstractions.Email;

namespace hr_sat.Tests;

// Hand-written fake for the Microsoft Account sign-in seam (issue 03, decision 31):
// NSubstitute was rejected for the stateful begin → pending → terminal flow, so the
// transitions are explicit knobs — SetPending/SetSucceeded/SetFailed/SetExpired — and
// calls are recorded for assertions. BeginConnectAsync lands the attempt in Pending,
// mirroring the real flow (begin always starts a pending attempt).
public sealed class FakeMicrosoftAccountSignIn : IMicrosoftAccountSignIn
{
    private static readonly DateTimeOffset ChallengeExpiresAt =
        new(2026, 9, 21, 12, 15, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset TokenExpiresOn =
        new(2026, 9, 21, 13, 0, 0, TimeSpan.Zero);

    private MicrosoftConnectStatus status = new(MicrosoftConnectState.Idle, null, null);

    public bool IsAvailable { get; set; } = true;

    public MicrosoftConnectChallenge BeginResult { get; set; } = new(
        "ABCD-EFGH",
        "https://microsoft.com/link",
        ChallengeExpiresAt);

    public MicrosoftAccountToken TokenResult { get; set; } = new(
        "fake-access-token",
        "anna@outlook.com",
        TokenExpiresOn);

    public Exception? TokenException { get; set; }

    public int BeginCallCount { get; private set; }

    public int AcquireTokenCallCount { get; private set; }

    public void Reset()
    {
        IsAvailable = true;
        BeginResult = new MicrosoftConnectChallenge(
            "ABCD-EFGH",
            "https://microsoft.com/link",
            ChallengeExpiresAt);
        TokenResult = new MicrosoftAccountToken(
            "fake-access-token",
            "anna@outlook.com",
            TokenExpiresOn);
        TokenException = null;
        BeginCallCount = 0;
        AcquireTokenCallCount = 0;
        status = new MicrosoftConnectStatus(MicrosoftConnectState.Idle, null, null);
    }

    public void SetPending() =>
        status = new MicrosoftConnectStatus(MicrosoftConnectState.Pending, null, null);

    public void SetSucceeded(string accountEmail) =>
        status = new MicrosoftConnectStatus(
            MicrosoftConnectState.Succeeded,
            accountEmail,
            null);

    public void SetFailed(string? error = null) =>
        status = new MicrosoftConnectStatus(
            MicrosoftConnectState.Failed,
            null,
            error ?? "The sign-in could not be completed — try again.");

    public void SetExpired() =>
        status = new MicrosoftConnectStatus(MicrosoftConnectState.Expired, null, null);

    public Task<MicrosoftConnectChallenge> BeginConnectAsync(CancellationToken cancellationToken)
    {
        BeginCallCount++;
        SetPending();
        return Task.FromResult(BeginResult);
    }

    public MicrosoftConnectStatus GetConnectStatus() => status;

    public Task<MicrosoftAccountToken> AcquireTokenAsync(CancellationToken cancellationToken)
    {
        AcquireTokenCallCount++;
        return TokenException is not null
            ? Task.FromException<MicrosoftAccountToken>(TokenException)
            : Task.FromResult(TokenResult);
    }
}
