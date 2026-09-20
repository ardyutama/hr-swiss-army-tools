namespace hr_sat.Application.Abstractions.Email;

// The effective SMTP Account as handlers see it: the password is always withheld
// (write-only, issue 02 decision 6) — HasPassword flags a stored one so the client can
// render "Saved — type to replace". Source names what actually sends mail (decision
// 10); when Source is None every field is null.
public sealed record SmtpSettingsSnapshot(
    string? Host,
    int? Port,
    string? Username,
    string? FromAddress,
    string? FromName,
    bool HasPassword,
    SmtpSettingsSource Source);
