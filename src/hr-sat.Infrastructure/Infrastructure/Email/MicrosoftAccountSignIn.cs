using System.Text;
using hr_sat.Application.Abstractions.Email;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Identity.Client;

namespace hr_sat.Infrastructure.Email;

// The Microsoft Account sign-in seam (issue 03, decisions 3, 17, 18): a singleton
// MSAL public client behind the Application-facing interface. Scopes are SMTP.Send
// plus offline_access/openid/email so the connect flow also derives the sender
// identity (decision 5). The persisted artifact is MSAL's serialized token cache,
// protected by the store under purpose smtp-microsoft-sign-in-v1 — a short-lived,
// encrypted blob, useless without the refresh path (decision 3).
//
// Grant persistence is stash-and-persist (decision 23): the AfterAccess cache callback
// stashes the serialized blob in memory and attempts a grant-only update that is a
// no-op while no Microsoft row exists; the connect completion path persists row +
// grant in one upsert. A process restart mid-connect simply loses the in-memory
// attempt — the status poll honestly reports Idle (decision 21).
public sealed class MicrosoftAccountSignIn : IMicrosoftAccountSignIn, IDisposable
{
    // decision 18: send scope + refresh + derived identity.
    private static readonly string[] Scopes =
    [
        "https://outlook.office.com/SMTP.Send",
        "offline_access",
        "openid",
        "email",
    ];

    private readonly IPublicClientApplication? msalApp;
    private readonly IServiceScopeFactory scopeFactory;
    private readonly ILogger<MicrosoftAccountSignIn> logger;
    private readonly Lock gate = new();

    private ConnectAttempt? attempt;
    private string? latestGrantBlob;

    public MicrosoftAccountSignIn(
        IOptions<SmtpOptions> options,
        IServiceScopeFactory scopeFactory,
        ILogger<MicrosoftAccountSignIn> logger)
    {
        this.scopeFactory = scopeFactory;
        this.logger = logger;

        // The project-owned public client (decision 2): tenant 'common' so M365
        // work/school accounts can sign in too. A public-client ID is not a secret;
        // when the key is absent the method is simply unavailable (decision 24).
        var clientId = options.Value.MicrosoftClientId;
        if (string.IsNullOrWhiteSpace(clientId))
        {
            return;
        }

        msalApp = PublicClientApplicationBuilder
            .Create(clientId)
            .WithAuthority(AzureCloudInstance.AzurePublic, "common")
            .Build();
        msalApp.UserTokenCache.SetBeforeAccessAsync(OnBeforeAccessAsync);
        msalApp.UserTokenCache.SetAfterAccessAsync(OnAfterAccessAsync);
    }

    public bool IsAvailable => msalApp is not null;

    public async Task<MicrosoftConnectChallenge> BeginConnectAsync(CancellationToken cancellationToken)
    {
        var app = msalApp ?? throw new InvalidOperationException(
            "Microsoft sign-in is not available; check IsAvailable before beginning a connect attempt.");

        var challengeReady = new TaskCompletionSource<MicrosoftConnectChallenge>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var current = new ConnectAttempt();
        lock (gate)
        {
            // A new begin replaces an in-flight attempt — last-write-wins (issue 02,
            // decision 30); a begin while connected is the reconnect path (decision 32).
            attempt?.Cancel();
            attempt = current;
        }

        var deviceCodeTask = app
            .AcquireTokenWithDeviceCode(
                Scopes,
                deviceCodeResult =>
                {
                    challengeReady.TrySetResult(new MicrosoftConnectChallenge(
                        deviceCodeResult.UserCode,
                        deviceCodeResult.VerificationUrl,
                        deviceCodeResult.ExpiresOn));
                    return Task.CompletedTask;
                })
            .ExecuteAsync(current.CancellationToken);

        // The device-code flow completes out of band, after the begin request has
        // answered with the challenge; the continuation persists the grant and settles
        // the attempt so the status poll sees the outcome.
        _ = CompleteAttemptAsync(current, deviceCodeTask, challengeReady);

        return await challengeReady.Task.WaitAsync(cancellationToken);
    }

    public MicrosoftConnectStatus GetConnectStatus()
    {
        lock (gate)
        {
            if (attempt is null)
            {
                return new MicrosoftConnectStatus(MicrosoftConnectState.Idle, null, null);
            }

            return attempt.State switch
            {
                MicrosoftConnectState.Pending => new MicrosoftConnectStatus(
                    MicrosoftConnectState.Pending, null, null),
                MicrosoftConnectState.Succeeded => new MicrosoftConnectStatus(
                    MicrosoftConnectState.Succeeded, attempt.AccountEmail, null),
                MicrosoftConnectState.Expired => new MicrosoftConnectStatus(
                    MicrosoftConnectState.Expired, null, null),
                _ => new MicrosoftConnectStatus(MicrosoftConnectState.Failed, null, attempt.Error),
            };
        }
    }

    public async Task<MicrosoftAccountToken> AcquireTokenAsync(CancellationToken cancellationToken)
    {
        var app = msalApp ?? throw new InvalidOperationException(
            "Microsoft sign-in is not available; the SMTP account cannot send without it.");

        // Silent acquisition (decision 18): the memory cache answers; the network is
        // hit only near expiry. No account cached → the grant is gone → UI-required.
        var account = (await app.GetAccountsAsync())
            .FirstOrDefault();
        if (account is null)
        {
            throw new MsalUiRequiredException(
                "no_cached_account",
                "No Microsoft account is connected; the token cache holds no account.");
        }

        var result = await app
            .AcquireTokenSilent(Scopes, account)
            .ExecuteAsync(cancellationToken);
        return new MicrosoftAccountToken(
            result.AccessToken,
            result.Account.Username,
            result.ExpiresOn);
    }

    public void Dispose()
    {
        lock (gate)
        {
            attempt?.Cancel();
            attempt = null;
        }
    }

    private async Task CompleteAttemptAsync(
        ConnectAttempt current,
        Task<AuthenticationResult> deviceCodeTask,
        TaskCompletionSource<MicrosoftConnectChallenge> challengeReady)
    {
        try
        {
            var result = await deviceCodeTask;
            var accountEmail = result.Account.Username;

            // A superseded attempt's late success persists nothing (last-write-wins,
            // decision 21) — the newer attempt owns the row.
            lock (gate)
            {
                if (!ReferenceEquals(attempt, current))
                {
                    return;
                }
            }

            // Connect is the test (decision 6): the row is written only on success,
            // grant included, in one upsert (decision 23). The AfterAccess stash
            // always holds the blob here — MSAL wrote the cache during acquisition.
            var grantBlob = latestGrantBlob;
            if (grantBlob is null)
            {
                throw new InvalidOperationException(
                    "The Microsoft sign-in completed without a token-cache write.");
            }

            await using var scope = scopeFactory.CreateAsyncScope();
            var store = scope.ServiceProvider.GetRequiredService<SmtpSettingsStore>();
            await store.ConnectMicrosoftAccountAsync(accountEmail, grantBlob, CancellationToken.None);
            current.Settle(MicrosoftConnectState.Succeeded, accountEmail, null);
        }
        catch (MsalServiceException exception)
        {
            challengeReady.TrySetException(exception);
            // decision 21's mapping: declined and expired name themselves; everything
            // else is a sanitized failure — raw MSAL text never crosses the wire.
            if (exception.ErrorCode == "expired_token")
            {
                current.Settle(MicrosoftConnectState.Expired, null, null);
            }
            else if (exception.ErrorCode == "authorization_declined")
            {
                current.Settle(
                    MicrosoftConnectState.Failed,
                    null,
                    "The sign-in was declined before it finished.");
            }
            else
            {
                logger.LogWarning(
                    "Microsoft connect attempt failed: {ErrorCode}",
                    exception.ErrorCode);
                current.Settle(
                    MicrosoftConnectState.Failed,
                    null,
                    "The sign-in could not be completed — try again.");
            }
        }
        catch (OperationCanceledException)
        {
            challengeReady.TrySetCanceled();
            // Replaced by a newer begin, or shutdown: leave the state to its owner.
        }
        catch (Exception exception)
        {
            challengeReady.TrySetException(exception);
            logger.LogWarning(exception, "Microsoft connect attempt failed unexpectedly.");
            current.Settle(
                MicrosoftConnectState.Failed,
                null,
                "The sign-in could not be completed — try again.");
        }
        finally
        {
            current.Dispose();
        }
    }

    // The token-cache read (decision 9): the stored grant blob feeds the cache. The
    // store unprotects; this class never sees ciphertext and never parses the blob.
    private async Task OnBeforeAccessAsync(TokenCacheNotificationArgs args)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var store = scope.ServiceProvider.GetRequiredService<SmtpSettingsStore>();
        var blob = await store.ReadMicrosoftGrantAsync(CancellationToken.None);
        if (blob is not null)
        {
            args.TokenCache.DeserializeMsalV3(Encoding.UTF8.GetBytes(blob));
        }
    }

    private async Task OnAfterAccessAsync(TokenCacheNotificationArgs args)
    {
        if (!args.HasStateChanged)
        {
            return;
        }

        latestGrantBlob = Encoding.UTF8.GetString(args.TokenCache.SerializeMsalV3());
        try
        {
            // Silent-refresh persistence (decision 23): grant-only, conditioned on the
            // row still being Microsoft-signed-in — a no-op during connect (the row is
            // absent or still app-password) and after a concurrent disconnect.
            await using var scope = scopeFactory.CreateAsyncScope();
            var store = scope.ServiceProvider.GetRequiredService<SmtpSettingsStore>();
            await store.UpdateMicrosoftGrantAsync(latestGrantBlob, CancellationToken.None);
        }
        catch (Exception exception)
        {
            // A lost cache write loses only the newest refresh token; the stored blob
            // still renews. Never fault the token operation over persistence.
            logger.LogWarning(exception, "Could not persist the Microsoft token cache.");
        }
    }

    // One connect attempt's mutable state. A settle lands only while the attempt is
    // still pending; a cancelled attempt's completion arrives as
    // OperationCanceledException and never settles (decision 21).
    private sealed class ConnectAttempt : IDisposable
    {
        private readonly CancellationTokenSource cancellation = new();
        private readonly object stateGate = new();
        private MicrosoftConnectState state = MicrosoftConnectState.Pending;
        private string? accountEmail;
        private string? error;

        public CancellationToken CancellationToken => cancellation.Token;

        public MicrosoftConnectState State
        {
            get { lock (stateGate) { return state; } }
        }

        public string? AccountEmail
        {
            get { lock (stateGate) { return accountEmail; } }
        }

        public string? Error
        {
            get { lock (stateGate) { return error; } }
        }

        public void Settle(MicrosoftConnectState terminal, string? email, string? failure)
        {
            lock (stateGate)
            {
                if (state != MicrosoftConnectState.Pending)
                {
                    return;
                }

                state = terminal;
                accountEmail = email;
                error = failure;
            }
        }

        public void Cancel() => cancellation.Cancel();

        public void Dispose() => cancellation.Dispose();
    }
}
