using hr_sat.Application.Abstractions.Email;
using MailKit.Net.Smtp;
using MailKit.Security;

namespace hr_sat.Infrastructure.Email;

// The Test connection probe (issue 02, decision 3): connect + authenticate against the
// values the user typed — persisting nothing — with the sender's ~15s timeout. A null
// request password falls back to the stored row's password (the upsert's
// keep-semantics applied to testing). Failure messages are stage-tagged and
// sanitized; raw exception text never crosses the wire (decision 12).
public sealed class MailKitSmtpConnectionTester(SmtpSettingsStore settingsStore) : ISmtpConnectionTester
{
    private static readonly TimeSpan HandshakeTimeout = TimeSpan.FromSeconds(15);

    public async Task<SmtpTestResult> TestAsync(
        SmtpTestRequest request,
        CancellationToken cancellationToken)
    {
        var password = request.Password
            ?? await StoredPasswordAsync(cancellationToken);
        if (password is null)
        {
            return SmtpTestResult.Failure("Enter the app password.");
        }

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(HandshakeTimeout);
        using var client = new SmtpClient();
        try
        {
            await client.ConnectAsync(
                request.Host,
                request.Port,
                SecureSocketOptions.Auto,
                timeout.Token);
            await client.AuthenticateAsync(request.Username, password, timeout.Token);
            await client.DisconnectAsync(true, timeout.Token);
            return SmtpTestResult.Success;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (AuthenticationException)
        {
            return SmtpTestResult.Failure("Authentication failed — check the app password.");
        }
        // Connect-stage failures, TLS handshake failures, and the handshake timeout all
        // land here: the honest stage-tagged message is that the host could not be
        // reached (decision 12).
        catch (Exception)
        {
            return SmtpTestResult.Failure($"Could not connect to {request.Host}:{request.Port}.");
        }
    }

    // The omitted-password fallback (decision 3): the saved row's own password, never
    // the configuration file's — the user is testing a potential replacement account.
    private async Task<string?> StoredPasswordAsync(CancellationToken cancellationToken) =>
        await settingsStore.ResolveStoredPasswordAsync(cancellationToken);
}
