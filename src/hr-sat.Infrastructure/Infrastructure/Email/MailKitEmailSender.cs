using hr_sat.Application.Abstractions.Email;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Options;
using MimeKit;

namespace hr_sat.Infrastructure.Email;

public sealed class MailKitEmailSender(IOptions<SmtpOptions> options) : IEmailSender
{
    // Per-send timeout: keeps a hung SMTP account from stalling the whole run — and the
    // round's advisory lock — beyond one bounded wait per candidate.
    private static readonly TimeSpan SendTimeout = TimeSpan.FromSeconds(15);

    public bool IsConfigured => options.Value.IsConfigured;

    public async Task SendAsync(
        string recipientEmail,
        string subject,
        string body,
        CancellationToken cancellationToken)
    {
        var smtp = options.Value;

        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(smtp.FromName ?? string.Empty, smtp.FromAddress!));
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
}
