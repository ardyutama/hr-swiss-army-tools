namespace hr_sat.Application.Abstractions.Email;

// The effective SMTP Account as handlers see it: the credential is always withheld
// (write-only, issue 02 decision 6) — HasPassword flags a stored one so the client can
// render "Saved — type to replace". SignInMethod names how the account authenticates
// (issue 03, decision 11): the saved row's method, App Password for the configuration
// file, null when nothing is configured. Source names what actually sends mail
// (decision 10); when Source is None every field is null.
public sealed record SmtpSettingsSnapshot(
    string? Host,
    int? Port,
    string? Username,
    string? FromAddress,
    string? FromName,
    bool HasPassword,
    SignInMethod? SignInMethod,
    SmtpSettingsSource Source);
