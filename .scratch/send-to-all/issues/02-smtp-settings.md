# 02: SMTP settings page (follow-up to 01-server-dispatch)

**What to build:** An in-app settings page for the installation's SMTP account, replacing
the config-file-only path from issue 01. Provider presets (Gmail / Outlook / Custom)
auto-fill host and port; the user pastes an app-specific password; a Test connection
button must pass before save is allowed. The amber refusal banner in the messaging
console gains a "Set up email sending" link to this page.

**Source:** Round 3 of the 01-server-dispatch implementation grill (decisions 27–31).
The Velopack deployment research (2026-09-15) showed the installer may be the HR user
themselves — "edit the config file" is a real burden on Windows (`%LocalAppData%`, JSON
syntax, app restart), so the UI earns its keep even though setup is one-time.

**Status:** design settled 2026-09-20 (round 1 decisions 1–8, round 2 decisions 9–16),
**user-confirmed 2026-09-20** — layout doc `02-smtp-settings.layout.md` written same
day; ready for implementation. Backend of issue 01 has landed (`SmtpOptions`,
`MailKitEmailSender`, `Dispatch.SmtpNotConfigured`); only the amber banner's link
waits on 01's client slice (`features/dispatches/` does not exist yet).

---

## Settled in the round-3 grill (see 01-server-dispatch.md decisions 27–31)

27. True auto-detection (email address → discovered settings) is dead: OAuth2 and
    app-specific passwords make the credential step provider-mandated user action.
    Achievable form = provider presets + app password, the Gitea/GitLab/Discourse pattern.
28. This page is its own issue — a new user flow (one flow = one slice), not a scope
    doubling of issue 01.
29. The messaging console's amber refusal copy keys off which config path exists:
    issue 01 alone → names the config file path; once this page lands → "Set up email
    sending" links here.
30. Governance: any HR user may edit (no roles exist to gate on); Test connection
    handshake must pass before save; last-write-wins on concurrent edits.
31. Per-installation account only — one shared account; per-user From addresses are a
    separate ADR-level pivot, out of scope here.

## Grill decisions — 2026-09-20 (round 1: storage, precedence, test, placement,
## presets, validation, history, glossary)

1. **Credential storage: ASP.NET Core Data Protection**, protector purpose
   `smtp-settings-v1`. Reversible as required (the password itself is sent to the SMTP
   server, so hashing is impossible). Protect/unprotect live inside the Infrastructure
   settings store — handlers never see ciphertext. Documented honest limit: any process
   running as the installing Windows user can unprotect; this is at-rest protection,
   not access control. Plain-text rejected (a reusable SMTP credential is a different
   leak class than the candidate PII already in the DB); a user-supplied passphrase
   rejected (a second secret to manage for a one-time setup, no threat-model win).
2. **Precedence: DB-wins, file-is-fallback, effect immediately on save.** A complete
   saved row is the effective config; an absent or incomplete row falls back to the
   `Smtp` config section (issue 01's bootstrap path). No restart — a settings page
   that still requires one has failed the issue's purpose. `MailKitEmailSender` moves
   from singleton-over-`IOptions` to per-send resolution in Infrastructure; the
   `IEmailSender` contract and the `Dispatch.SmtpNotConfigured` refusal code are
   unchanged, so the console's client copy keeps keying off the same code. "File
   wins" rejected: it would make the page a write-only dead end whenever a leftover
   file section exists.
3. **Test connection: `POST /api/settings/smtp/test` takes the unsaved form values**
   (test what the user typed) and persists nothing. The handshake is connect +
   authenticate only — decision 30's wording — with a ~15s timeout mirroring the
   sender. A real probe email (Gitea pattern) is rejected: it forces mailbox-watching
   before save, and the From-alias trap it catches is rare under the preset flow
   (Gmail/Outlook usernames *are* the From); the dispatch report is the honest
   backstop. Save is gated client-side on a passing test of the *current* values;
   any edit after a pass returns the form to must-test-again. The server does not
   enforce test-before-save.
4. **Placement: the sidebar gains a "Settings" label group** with one item, "Email
   sending" → `/settings/email`. Gear-in-header rejected (no desktop header chrome
   exists — the shell's navbar is mobile-only; adding a bar for one icon is inverted
   priorities). Banner-only rejected (the page becomes undiscoverable the moment SMTP
   is healthy — no-dead-UI cuts both ways). This is the app's first settings surface,
   not its last. Client slice: `src/pages/settings/` + `src/features/email-settings/`.
5. **Three presets only: Gmail, Outlook, Custom.** Gmail fills
   `smtp.gmail.com:587` with helper "requires 2FA → app password"; Outlook fills
   `smtp.office365.com:587` with helper "app password via account security"; Custom
   leaves host/port blank. No "Office 365 legacy basic auth" preset — Microsoft
   retired SMTP basic auth by 2026, so that preset would ship a dead end; the Outlook
   helper text absorbs the question. On-prem Exchange and everything else is what
   Custom is for.
6. **Validation set.** Host: bare hostname or IP, no scheme, no slashes. Port: integer
   1–65535, preset-filled, editable. Username: non-empty; strict email shape enforced
   only under the Gmail/Outlook presets, free text under Custom. Password: required
   and **write-only** — GET never returns it (file-sourced or not); the field shows a
   saved-state placeholder ("Saved — type to replace") and submits only when typed
   into; no reveal-on-click, which would reduce decision 1's protection to
   obfuscation. FromAddress: strict email format. FromName: optional, length-capped.
   TLS mode is **not exposed** — the sender negotiates via `SecureSocketOptions.Auto`
   and no observed failure mode justifies the knob. Client `validation.ts` holds input
   rules; the server validator mirrors them and owns business rules.
7. **Editing while runs exist: the `dispatches` snapshot gains `from_address`** (one
   small migration over issue 01's just-landed table), extending decision 9's snapshot
   principle — "which account sent this" stays answerable months later like the rest
   of the snapshot. The settings page shows no "past sends came from the old account"
   banner; the record answers the question when someone actually asks it.
8. **Glossary: `SMTP Account` added to CONTEXT.md this session** (the per-installation
   account, managed on the settings page; avoid: sender profile, per-user account,
   mailbox). The Dispatch entry already used the phrase; the sidebar copy stays
   "Email sending" — UI labels aren't glossary terms.

Layout doc (`02-smtp-settings.layout.md`) written same session; design fully settled,
implementation not started.

## Grill decisions — 2026-09-20 (round 2: prefill, status, escape hatch, API,
## composition, slice order)

9. **First-open prefill from the config file.** When no saved row exists but an
   `Smtp` config section is present, the form pre-fills host, port, username,
   from-address and from-name from it; the password field is never pre-filled (write-only,
   decision 6). A muted note reads: "Currently in effect from the configuration file —
   saving here replaces it."
10. **Effective-source status line.** A persistent muted line above the form states
    what actually sends mail: "Sending as hr@firma.example via smtp.gmail.com:587 —
    saved on this page" / "…— from the configuration file" / "Email sending is not
    configured." After a save it updates in place — that is the success feedback, so no
    toast anywhere (ADR-0008 d16).
11. **Escape hatch: `DELETE /api/settings/smtp`.** A quiet "Remove saved settings"
    action rendered only when a saved row exists, behind a confirm dialog; after removal
    the file section (if any) resumes. Without it the file can never take effect again
    once a row exists — a support trap for the exact scenario the issue was born to fix.
12. **Endpoint surface and failure contract.** `GET /api/settings/smtp` → always 200:
    `{ host, port, username, fromAddress, fromName, hasPassword, source:
    'settings' | 'configuration-file' | 'none' }` — `none` means null fields; never
    returns the password. `PUT /api/settings/smtp` — upsert; omitted password keeps the
    stored one; per-field 400s in the existing validation style.
    `POST /api/settings/smtp/test` — unsaved values (decision 3); pass → 200; fail →
    400 with stable code `EmailSettings.TestFailed` and a stage-tagged, sanitized
    message ("Could not connect to smtp.gmail.com:587" / "Authentication failed —
    check the app password"); raw exception text never crosses the wire.
    Backend: `Features/EmailSettings/{GetSmtpSettings, TestSmtpConnection,
    UpsertSmtpSettings, RemoveSmtpSettings}/` + matching `Endpoints/EmailSettings/`.
13. **Saving mid-run: resolve once per run.** The account is part of the run's mental
    model, like the templates it rendered. A send or retry handler invocation resolves
    the effective config once at the top; the test endpoint and the status line still
    resolve live. Decision 7's `from_address` column then shows a single sender per
    run. Per-send resolution rejected: a mixed-account run is confusing history nobody
    asked for.
14. **Banner copy and ownership.** Copy settled now; whoever implements *second* ships
    the link — if 01's client lands first, this issue adds the link; if this page lands
    first, 01's client ships the linked copy from day one. Copy: the refusal sentence
    stands, with a trailing action link "Set up email sending" → `/settings/email`.
    The banner copy lives in `01-server-dispatch.layout.md` section C.
15. **Form composition defaults** (layout doc pins the full mock): preset selector =
    three radio cards (URadioGroup) with helper text per card (Gmail: "Requires 2FA →
    create an app password"; Outlook: "Create an app password in account security";
    Custom: "Any SMTP host"); inputs per the data/form pattern (label above, helper
    text, error below, `gap-2`); actions row at bottom: Test connection (secondary)
    then Save (primary, disabled until a passing test of the current values); test
    result = inline status block under the actions with four states (idle / testing /
    success / failure with the server's sanitized message); save success = the Q10
    status line updating.
16. **Slice order** (mirrors 01's decision 17): migration (`smtp_settings` table +
    `dispatches.from_address`) → Infrastructure settings store (EF +
    `IDataProtector`, purpose `smtp-settings-v1`) + effective-config resolver with
    run-scoped pinning (decision 13) + `MailKitEmailSender` re-registration +
    `AddDataProtection()` wiring (new package reference per ADR-0012) → GetSmtpSettings
    → TestSmtpConnection → UpsertSmtpSettings + RemoveSmtpSettings → client slice
    (`features/email-settings/`, `pages/settings/`, sidebar group) → banner link if
    unblocked (decision 14). Tests land per slice.

Layout doc (`02-smtp-settings.layout.md`) written same session; design fully settled,
implementation not started.
