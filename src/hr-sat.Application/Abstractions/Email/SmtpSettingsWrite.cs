namespace hr_sat.Application.Abstractions.Email;

// The values an SMTP Account upsert persists, discriminated by Sign-in Method (issue
// 03, decision 29): an App Password save writes the full credential set and clears the
// stored grant; a Microsoft Account save writes only the From fields and clears the
// stored password — host, port, and username stay as the connect flow derived them,
// and the grant stays untouched. Under App Password a null Password keeps the stored
// one (issue 02, decision 12). Plaintext crosses into the Infrastructure store and is
// protected there before it touches the database.
public abstract record SmtpSettingsWrite
{
    public sealed record AppPassword(
        string Host,
        int Port,
        string Username,
        string? Password,
        string FromAddress,
        string? FromName) : SmtpSettingsWrite;

    public sealed record MicrosoftAccount(
        string FromAddress,
        string? FromName) : SmtpSettingsWrite;
}
