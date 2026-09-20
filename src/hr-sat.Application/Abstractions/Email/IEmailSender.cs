namespace hr_sat.Application.Abstractions.Email;

// Outbound mail boundary (ADR-0019): the installation's own SMTP account. IsConfigured
// is false when the Smtp configuration section is absent or effectively empty; handlers
// refuse before writing any dispatch state in that case.
public interface IEmailSender
{
    bool IsConfigured { get; }

    Task SendAsync(
        string recipientEmail,
        string subject,
        string body,
        CancellationToken cancellationToken);
}
