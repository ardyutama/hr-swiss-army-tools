namespace hr_sat.Application.Abstractions.Email;

// The values an SMTP Account upsert persists. The password crosses into the
// Infrastructure store as plaintext and is protected there before it touches the
// database; a null password keeps the stored one (issue 02, decision 12).
public sealed record SmtpSettingsWrite(
    string Host,
    int Port,
    string Username,
    string? Password,
    string FromAddress,
    string? FromName);
