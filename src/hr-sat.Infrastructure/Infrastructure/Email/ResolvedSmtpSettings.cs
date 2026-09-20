using hr_sat.Application.Abstractions.Email;

namespace hr_sat.Infrastructure.Email;

// The store's one resolution result per scope: the handler-facing snapshot (password
// withheld), the send-facing connection (effective password unprotected, null when
// the installation cannot send), and the saved row's own password (unprotected) for
// the test endpoint's omitted-password fallback.
internal sealed record ResolvedSmtpSettings(
    SmtpSettingsSnapshot Snapshot,
    EffectiveSmtpConnection? Connection,
    string? StoredPassword);
