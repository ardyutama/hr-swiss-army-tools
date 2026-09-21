namespace hr_sat.Application.Abstractions.Email;

// Outbound mail boundary (ADR-0019): the installation's own SMTP account.
// CheckReadinessAsync answers whether a run may start — NotConfigured when no
// effective account exists, SignInExpired when a Microsoft Account grant no longer
// renews (issue 03, decision 20); handlers refuse before writing any dispatch state
// in those cases.
public interface IEmailSender
{
    Task<SmtpReadiness> CheckReadinessAsync(CancellationToken cancellationToken);

    Task SendAsync(
        string recipientEmail,
        string subject,
        string body,
        CancellationToken cancellationToken);
}
