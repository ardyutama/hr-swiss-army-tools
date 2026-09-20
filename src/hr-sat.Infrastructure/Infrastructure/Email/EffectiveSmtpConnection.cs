namespace hr_sat.Infrastructure.Email;

// The fully-resolved connection values a send or test runs with, password unprotected.
// Never leaves Infrastructure.
internal sealed record EffectiveSmtpConnection(
    string Host,
    int Port,
    string Username,
    string Password,
    string FromAddress,
    string? FromName);
