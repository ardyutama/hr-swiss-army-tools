namespace hr_sat.Infrastructure.Email;

// The fully-resolved connection values a send runs with, discriminated by Sign-in
// Method (issue 03, decision 19): an App Password connection authenticates with the
// unprotected password; a Microsoft Account connection carries only the account email
// — the token is acquired per send through IMicrosoftAccountSignIn and never stored
// here. The union makes "a password present under microsoft-account"
// unconstructible. Never leaves Infrastructure.
internal abstract record EffectiveSmtpConnection(
    string Host,
    int Port,
    string FromAddress,
    string? FromName)
{
    public sealed record AppPassword(
        string Host,
        int Port,
        string Username,
        string Password,
        string FromAddress,
        string? FromName)
        : EffectiveSmtpConnection(Host, Port, FromAddress, FromName);

    public sealed record MicrosoftAccount(
        string Host,
        int Port,
        string AccountEmail,
        string FromAddress,
        string? FromName)
        : EffectiveSmtpConnection(Host, Port, FromAddress, FromName);
}
