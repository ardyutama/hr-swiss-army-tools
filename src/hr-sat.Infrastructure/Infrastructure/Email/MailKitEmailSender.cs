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
public sealed class MailKitEmailSender(SmtpSettingsStore settingsStore) : IEmailSender
{
    // Per-send timeout: keeps a hung SMTP account from stalling the whole run — and the
    // round's advisory lock — beyond one bounded wait per candidate.
    private static readonly TimeSpan SendTimeout = TimeSpan.FromSeconds(15);

    // The IEmailSender contract is synchronous (ADR-0019 seam, unchanged per decision
    // 2); resolution is one bounded read per request, pinned by the store.
    public bool IsConfigured => Resolve() is not null;

    public async Task SendAsync(
        string recipientEmail,
        string subject,
        string body,
        CancellationToken cancellationToken)
    {
        var smtp = Resolve() ?? throw new InvalidOperationException(
            "SMTP is not configured; check IsConfigured before sending.");

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
        await client.AuthenticateAsync(smtp.Username, smtp.Password, timeout.Token);
        await client.SendAsync(message, timeout.Token);
        await client.DisconnectAsync(true, timeout.Token);
    }

    private EffectiveSmtpConnection? Resolve() =>
        settingsStore
            .ResolveConnectionAsync(CancellationToken.None)
            .GetAwaiter()
            .GetResult();
}
