using hr_sat.Application.Abstractions.Email;

namespace hr_sat.Infrastructure.Email;

// The store's one resolution result per scope: the handler-facing snapshot (credential
// withheld), the send-facing connection (effective credential materialized, null when
// the installation cannot send), the saved row's own password (unprotected) for the
// test endpoint's omitted-password fallback, and the saved row's grant blob (raw MSAL
// serialization, unprotected) for the token-cache callback's read (issue 03,
// decision 23).
internal sealed record ResolvedSmtpSettings(
    SmtpSettingsSnapshot Snapshot,
    EffectiveSmtpConnection? Connection,
    string? StoredPassword,
    string? StoredGrant);
