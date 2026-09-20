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

**Status:** design settled 2026-09-20 (decisions 27–31 recorded in
`01-server-dispatch.md`), **user-confirmed 2026-09-20** — implementation not started;
blocked by issue 01 (the options abstraction it overrides ships there). Open questions
below need this issue's own grill before implementation.

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

## Open questions for this issue's own grill (not yet asked)

- **Credential storage.** The SMTP password now lives in the domain DB: encrypted at
  rest (with what key, stored where?), or plain with a documented threat note?
  Reversible encryption is required either way — the password itself must be sent to
  the SMTP server, so hashing is impossible.
- **Precedence.** DB settings override the config file when both exist (file becomes
  bootstrap/fallback), or the file wins (DB ignored until file section removed)?
- **Test-connection endpoint shape.** `POST /api/settings/smtp/test` with the *unsaved*
  form values (test what the user typed), or test-on-save only?
- **Where the page lives.** The app's first settings surface: sidebar entry (ADR-0008
  decision 15's "no dead UI" means the link only appears when the page exists), a
  gear icon in the header, or reachable only from the amber banner?
- **Provider preset list.** Gmail + Outlook + Custom covers the likely 95%; does
  "Office 365 SMTP (legacy basic auth)" deserve its own preset or a note under Outlook?
- **Validation rules.** Host format, port range, From-address must be a mailbox the
  account can actually send as (verifiable via test connection?), TLS mode
  (auto/always/starttls).
- **Editing while runs exist.** Changing the account mid-history is legal (dispatches
  carry their own snapshots); does the UI note that past sends came from the old
  account, or stay silent?
- **Layout doc.** Needed (this is real UI shape): form layout per the skill's data/form
  patterns (label above input, error below, helper text), preset selector, test-connection
  states (idle/testing/success/failure with server error text), save gating.
