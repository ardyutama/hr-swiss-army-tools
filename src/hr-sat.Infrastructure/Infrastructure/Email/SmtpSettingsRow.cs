using hr_sat.Application.Abstractions.Email;

namespace hr_sat.Infrastructure.Email;

// Persistence row for the installation's SMTP Account. Installation configuration
// stored in the database, not domain data (ADR-0019) — an Infrastructure row rather
// than a domain entity. ProtectedPassword holds Data Protection ciphertext; plaintext
// never leaves SmtpSettingsStore (issue 02, decision 1). SignInMethod discriminates
// the credential the row carries: ProtectedPassword under App Password, or
// ProtectedGrant — the serialized MSAL token cache — under Microsoft Account; the
// other method's credential is always null (issue 03, decisions 4, 16, 28).
public sealed class SmtpSettingsRow
{
    public long Id { get; set; }
    public required string Host { get; set; }
    public int Port { get; set; }
    public required string Username { get; set; }
    public SignInMethod SignInMethod { get; set; }
    public string? ProtectedPassword { get; set; }
    public string? ProtectedGrant { get; set; }
    public required string FromAddress { get; set; }
    public string? FromName { get; set; }
}
