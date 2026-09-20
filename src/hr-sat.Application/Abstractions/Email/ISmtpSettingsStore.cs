namespace hr_sat.Application.Abstractions.Email;

// The SMTP Account persistence boundary (issue 02, decisions 1–2): Infrastructure owns
// the EF row, Data Protection protect/unprotect, and the configuration-file fallback,
// so handlers never see ciphertext and never read configuration directly. Resolution
// pins once per calling scope (decision 13) — a dispatch run sees one account; the
// settings page's own scope keeps its status line live.
public interface ISmtpSettingsStore
{
    Task<SmtpSettingsSnapshot> GetSnapshotAsync(CancellationToken cancellationToken);

    // Whether the saved row holds a password — drives the test endpoint's
    // omitted-password fallback (same keep-semantics as the upsert, decision 12).
    Task<bool> HasStoredPasswordAsync(CancellationToken cancellationToken);

    Task SaveAsync(SmtpSettingsWrite write, CancellationToken cancellationToken);

    Task RemoveAsync(CancellationToken cancellationToken);
}
