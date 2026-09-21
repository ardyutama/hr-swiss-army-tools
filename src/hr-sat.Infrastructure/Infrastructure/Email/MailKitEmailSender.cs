using hr_sat.Application.Abstractions.Email;
using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;

namespace hr_sat.Infrastructure.Email;

// Sends through the installation's effective SMTP Account (issue 02, decision 2): the
// saved settings row when complete, else the Smtp configuration section. Registered
// scoped; the store pins one resolution per scope, so a dispatch run sees a single
// account from the refusal check to the last send (decision 13), and a settings save
// takes effect on the next request — no restart.
//
// The auth path follows the connection's Sign-in Method (issue 03, decision 19): an
// App Password connection authenticates with the stored password; a Microsoft Account
// connection acquires a token per send via MSAL silent (memory-cached, network only
// near expiry — decision 12) and authenticates with XOAUTH2. We never cache tokens
// ourselves.
public sealed class MailKitEmailSender(
    SmtpSettingsStore settingsStore,
    IMicrosoftAccountSignIn microsoftSignIn) : IEmailSender
{
    // Per-send timeout: keeps a hung SMTP account from stalling the whole run — and the
    // round's advisory lock — beyond one bounded wait per candidate.
    private static readonly TimeSpan SendTimeout = TimeSpan.FromSeconds(15);

    // The dispatch pre-flight (issue 03, decision 20): password auth is completeness
    // only — no network; Microsoft Account auth performs one silent acquisition, and
    // any failure there (revoked grant or not) reads SignInExpired — an amber refusal
    // beats dispatching into guaranteed per-send failures.
    public async Task<SmtpReadiness> CheckReadinessAsync(CancellationToken cancellationToken)
    {
        var connection = await settingsStore.ResolveConnectionAsync(cancellationToken);
        switch (connection)
        {
            case null:
                return SmtpReadiness.NotConfigured;
            case EffectiveSmtpConnection.AppPassword:
                return SmtpReadiness.Configured;
            case EffectiveSmtpConnection.MicrosoftAccount:
                try
                {
                    await microsoftSignIn.AcquireTokenAsync(cancellationToken);
                    return SmtpReadiness.Configured;
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception)
                {
                    return SmtpReadiness.SignInExpired;
                }

            default:
                throw new InvalidOperationException(
                    $"Unknown connection type '{connection.GetType().Name}'.");
        }
    }

    public async Task SendAsync(
        string recipientEmail,
        string subject,
        string body,
        CancellationToken cancellationToken)
    {
        var smtp = await settingsStore.ResolveConnectionAsync(cancellationToken)
            ?? throw new InvalidOperationException(
                "SMTP is not configured; check CheckReadinessAsync before sending.");

        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(smtp.FromName ?? string.Empty, smtp.FromAddress));
        message.To.Add(new MailboxAddress(string.Empty, recipientEmail));
        message.Subject = subject;
        message.Body = new TextPart("plain") { Text = body };

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(SendTimeout);
        using var client = new SmtpClient();
        await client.ConnectAsync(
            smtp.Host,
            smtp.Port,
            SecureSocketOptions.Auto,
            timeout.Token);
        switch (smtp)
        {
            case EffectiveSmtpConnection.AppPassword appPassword:
                await client.AuthenticateAsync(
                    appPassword.Username,
                    appPassword.Password,
                    timeout.Token);
                break;
            case EffectiveSmtpConnection.MicrosoftAccount microsoftAccount:
            {
                // A revocation between the pre-flight and here fails this send with
                // sanitized auth copy; retry semantics are unchanged (decision 12).
                var token = await microsoftSignIn.AcquireTokenAsync(timeout.Token);
                await client.AuthenticateAsync(
                    new SaslMechanismOAuth2(microsoftAccount.AccountEmail, token.AccessToken),
                    timeout.Token);
                break;
            }
        }

        await client.SendAsync(message, timeout.Token);
        await client.DisconnectAsync(true, timeout.Token);
    }
}
