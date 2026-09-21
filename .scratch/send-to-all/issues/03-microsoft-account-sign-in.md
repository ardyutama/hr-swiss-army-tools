# 03: Microsoft Account sign-in for the SMTP account (follow-up to 02-smtp-settings)

**What to build:** A second Sign-in Method for the SMTP Account — connecting a
Microsoft Account through its OAuth2 device-code flow instead of typing an app
password — because Microsoft retired basic auth for personal Outlook.com accounts, so
the password-only path from issue 02 is a proven dead end for them. Workstream A copy
fixes fold in: the `outlook` preset help and the appsettings comment block currently
point users at app-password guidance that no longer exists for personal accounts.

**Source:** the SMTP OAuth2 handoff (2026-09-21) — diagnosis proved live that personal
Outlook.com refuses `AUTH LOGIN` (`535 5.7.3 Authentication unsuccessful`) while
advertising `AUTH LOGIN XOAUTH2`; Thunderbird works against the same server because it
negotiates OAuth2. Capability gap, not a logic bug — no seam regression-test under the
repo's diagnosis rules.

**Status:** design settled 2026-09-21 (round 1 decisions 1–8, round 2 decisions 9–15),
**user-confirmed 2026-09-21**; implementation grill settled 2026-09-21 (rounds 3–4
decisions 16–38, user-confirmed "all recommended" both rounds) — frontier empty;
ready for implementation in the Q13 slice order.

---

## Grill decisions — 2026-09-21 (round 1: provider scope, app registration, MSAL,
## schema shape, sender identity, connect-as-test, dispatch pre-flight, copy fixes)

1. **Provider scope: Microsoft only.** The `outlook` preset gains the OAuth2 path;
   Gmail stays app-password (still works today); Custom stays password-shaped — its
   audience (on-prem Exchange, ISP relays) is password-shaped too. The sign-in-method
   seam is designed so a second provider is additive later, but one ships.
2. **Project-owned public client, tenant `common`.** The client ID ships in
   `appsettings.json` (a public-client ID is not a secret, and the file makes it
   overridable per installation); no override UI. `common` lets M365 work/school
   accounts sign in too, though basic auth remains their simpler path.
3. **MSAL.NET** (`Microsoft.Identity.Client`) for device-code acquisition and silent
   refresh, behind an interface so tests substitute it — no live Microsoft calls in
   the suite. The persisted artifact is MSAL's serialized token cache blob
   (encrypted), which honestly relaxes the handoff's "never store an access token
   long-term": it's short-lived, encrypted, and useless without the refresh path.
   Hand-rolled OAuth2 rejected: polling, `slow_down`, rotation races, and expiry math
   are exactly where homegrown auth rots. New package per ADR-0012; rationale lands in
   this feature's ADR.
4. **Schema: discriminator on the same singleton row.** `smtp_settings` gains
   `auth_method` (`password | microsoft` — domain terms, not `basic | oauth2`) plus
   nullable `protected_grant`; `protected_password` becomes nullable. Completeness in
   `SmtpSettingsStore.LoadAsync` is evaluated per method. `EffectiveSmtpConnection`
   becomes an auth union — password, or acquire-token + account email — never exposed
   in Application DTOs beyond the method enum the client renders. A separate grant
   table rejected: ceremony without a second consumer on a singleton row.
5. **Sender identity is derived, not typed, under Microsoft sign-in.** Request
   `openid email` alongside `SMTP.Send` + `offline_access`; the derived account email
   is stored as the username and pre-fills FromAddress (still editable). "Connected as
   anna@outlook.com" is the reassurance line the settings page shows.
6. **Connect is the test.** The connect flow completes authentication before anything
   is saved; the row is written only on success. No test-after-connect button — the
   dispatch pre-flight (decision 7) is the backstop. Test connection stays an
   App-Password-method concept. Glossary: **Sign-in Method** with values **App
   Password** and **Microsoft Account** added to CONTEXT.md this session; `basic |
   oauth2` stays wire protocol, out of domain language.
7. **Dispatch pre-flight refusal on a dead grant.** The dispatch handler silently
   refreshes once at run start; failure refuses the whole run amber ("Reconnect the
   email account on the settings page"), no dispatch rows written. A wall of identical
   per-dispatch failures rejected; retry semantics unchanged.
8. **OAuth2 is settings-page-only; workstream A folds in.** The `Smtp` config section
   keeps implying password auth — a refresh token in a file can't track rotation. The
   `outlook` preset help in `validation.ts` and the Outlook comment block in
   `appsettings.json` are reworded in this issue (Microsoft Account first; basic auth
   = M365 work/school with SMTP AUTH enabled, being retired).

## Grill decisions — 2026-09-21 (round 2: grant mechanics, connect flow, API
## contract, sender path, slices, tests, ADR)

9. **Grant storage: MSAL token cache blob under its own protector purpose.** A
   singleton `IPublicClientApplication` lives in Infrastructure behind an
   Application-facing sign-in interface (begin-connect + acquire-silent). MSAL cache
   callbacks read/write `protected_grant` through the existing store, encrypted under
   purpose `smtp-microsoft-sign-in-v1` (sibling to `smtp-settings-v1`). The store
   stays MSAL-ignorant (opaque blob in/out); cache writes fire on connect and on
   every silent refresh — credential data, never review data, so mid-run writes are
   legal. Raw-refresh-token extraction (impossible with MSAL) and purpose reuse
   rejected.
10. **Connect flow: begin/status pair, immediate write on success.**
    `POST /api/settings/smtp/microsoft-connect/begin` starts an in-memory attempt
    (single process; a new begin replaces an in-flight one — last-write-wins, issue 02
    decision 30) and returns `{ userCode, verificationUrl, expiresAt }` from MSAL's
    device-code callback. `GET .../microsoft-connect/status` → `{ state: 'pending' |
    'succeeded' | 'failed' | 'expired', accountEmail?, error? }`; the client polls
    ~2s to terminal. Process restart mid-connect = no attempt = honest status. On
    success the row's Microsoft columns write immediately (decision 6): `auth_method`,
    grant, username = derived email, host/port = outlook preset constants; FromAddress
    is **overwritten** with the derived email, FromName untouched. UI: Microsoft
    Account radio swaps host/port/username/password for a connect panel (copyable
    code, URL, waiting status, expiry) → "Connected as anna@outlook.com" + Disconnect;
    From fields stay visible. No cancel endpoint; copy notes revocation lives in the
    user's Microsoft account settings.
11. **GET/PUT contract with the discriminator.** GET gains `authMethod:
    'password' | 'microsoft' | null` (config-file source → `password`); the derived
    email already rides in `username`. PUT gains `authMethod`: `password` → issue-02
    validation unchanged and **clears the grant**; `microsoft` → requires an existing
    grant, validates From fields only, **clears the stored password**. Method switches
    discard the other credential server-side; the client confirms before switching
    away from a connected account. Test endpoint stays password-only; Microsoft mode
    hides the Test button and enables Save once connected (decision 6). Client
    `validation.ts` becomes method-conditional; the server validator mirrors.
12. **Sender token path and readiness seam.** `IEmailSender` gains
    `CheckReadinessAsync` → `Configured | NotConfigured | SignInExpired`; the
    Microsoft path performs one silent token acquisition. The dispatch handler refuses
    the run on `SignInExpired` with code `Dispatch.SmtpSignInExpired` and amber
    "Reconnect the email account on the settings page" (link per issue 02 decision 14
    when the console client lands). Sends acquire a token per send via MSAL silent
    (memory-cached; network only near expiry) then `SaslMechanismOAuth2(accountEmail,
    token)`; no token caching of our own. Revocation between pre-flight and send →
    that dispatch fails with sanitized auth copy; retry semantics unchanged.
13. **Migration and slice order.** Migration: `auth_method` text not null default
    `'password'`; `protected_grant` text nullable; `protected_password` → nullable;
    existing rows land on `password`. Re-add the hand-maintained `BuildSnapshotModel`
    helper after `dotnet ef migrations add` (repo gotcha). Slices, tests landing per
    slice: (1) migration + store completeness-per-method + `EffectiveSmtpConnection`
    union; (2) MSAL seam (interface, Infrastructure implementation, cache-callback
    persistence, DI + ADR-0012 package note); (3) connect endpoints; (4) GET/PUT
    auth-method through incl. credential-discarding transitions; (5) sender OAuth2
    path + readiness seam + dispatch refusal code; (6) client slice (method radio,
    connect panel, method-conditional validation, preset help fix, seam spec);
    (7) appsettings comment reword + client-ID key; (8) ADR-0020.
14. **Test inventory.** Server: begin/status seam tests against a fake sign-in
    interface (success / user declines / expiry — no live Microsoft calls); GET
    surfacing `authMethod`; PUT method-switch discarding the other credential; store
    completeness-per-method (unreadable grant → file fallback, same as today's
    unreadable password); dispatch pre-flight refusal emitting
    `Dispatch.SmtpSignInExpired`; sender orchestration with fake token acquisition.
    MailKit SASL bytes stay below the seam, untested directly. Client seam spec:
    method radio switching, connect panel states (pending → succeeded / failed /
    expired), save gating per method, preset help copy.
15. **ADR-0020 at implementation time:** "Microsoft Account sign-in for the SMTP
    Account", amending ADR-0019 point 2 — installation configuration now includes a
    stored grant, not just appsettings credentials. CONTEXT.md already carries the
    **Sign-in Method** glossary entry (2026-09-21).

---

## Implementation grill — 2026-09-21 (round 3: wire names, MSAL seam, contract
## semantics, UX structure; all confirmed "all recommended")

16. **Wire names use glossary language, amending decision 4.** Column
    `sign_in_method` (not `auth_method`), DTO field `signInMethod` (not
    `authMethod`), values `app-password | microsoft-account` (not
    `password | microsoft`). Rationale: the **Sign-in Method** glossary entry bans
    "auth method"; the wire speaks the glossary.
17. **One Application-facing interface** `IMicrosoftAccountSignIn` in
    `Application/Abstractions/Email/`: `BeginConnectAsync(ct) →
    MicrosoftConnectChallenge(userCode, verificationUrl, expiresAt)`; synchronous
    `GetConnectStatus() → MicrosoftConnectStatus(state, accountEmail?, error?)`
    (reads the singleton attempt holder, no I/O at poll time); `AcquireTokenAsync(ct)
    → MicrosoftAccountToken(accessToken, accountEmail, expiresOn)` (silent). Split
    into two interfaces deferred until the sender's token path wants a narrower fake.
18. **MSAL `IPublicClientApplication` singleton** in Infrastructure; token-cache
    callbacks via `SetBeforeAccessAsync`/`SetAfterAccessAsync`, writes only when
    `args.HasStateChanged`, blob encrypted under purpose `smtp-microsoft-sign-in-v1`
    (sibling to `smtp-settings-v1`). Scopes: `["https://outlook.office.com/SMTP.Send",
    "offline_access", "openid", "email"]`; silent acquire = `GetAccountsAsync()` →
    first account → `AcquireTokenSilent`. (Persistence mechanism amended by decision
    23 after a schema race surfaced.)
19. **`EffectiveSmtpConnection` becomes an abstract record union**, internal to
    Infrastructure: `Password(host, port, username, password, fromAddress, fromName)`
    | `MicrosoftAccount(host, port, accountEmail, fromAddress, fromName)` — the flat
    record + enum was rejected because it re-invites "password present under
    microsoft" states. `MailKitEmailSender` takes `IMicrosoftAccountSignIn` and
    switches: password → `AuthenticateAsync(user, pass)`; microsoft →
    `AcquireTokenAsync` → `AuthenticateAsync(new SaslMechanismOAuth2(email, token))`.
20. **`IsConfigured` is replaced** on `IEmailSender` by
    `Task<SmtpReadiness> CheckReadinessAsync(CancellationToken)` returning
    `Configured | NotConfigured | SignInExpired` (enum in Application) — one
    readiness concept; the sync property's only consumer was the dispatch pre-flight.
    Password path = completeness only, no network; microsoft path = one silent
    acquisition, `MsalUiRequiredException` → `SignInExpired`; non-UI-required MSAL
    failures (e.g. network) also → `SignInExpired` — amber refusal beats dispatching
    into guaranteed per-send failures.
21. **Connect status enum gains `idle`** (no attempt exists — first visit, process
    restart): wire `idle | pending | succeeded | failed | expired`, mirrored in the
    client union. MSAL mapping: `authorization_declined` → `failed`,
    `expired_token`/device-code timeout → `expired`, all else → `failed` with
    sanitized copy. Terminal results retained on the singleton until the next begin
    so a late poll still sees the outcome.
22. **PUT in microsoft mode is ignore-and-derive**: server derives
    host/port/username from the connected account, validates only From fields,
    silently ignores credential fields the client shouldn't have sent; missing or
    unreadable grant under microsoft → 400 validation problem naming "connect a
    Microsoft account first" (not 409 — input invalidity, not lifecycle conflict).
23. **Grant persistence = stash-and-persist** (amends decision 18's mechanism; a
    direct callback write races the NOT NULL columns and an absent row):
    `AfterAccess` stashes the serialized blob in memory on the singleton; the connect
    completion path persists row + grant in **one upsert**; on later silent refreshes
    (row guaranteed) the callback persists a `protected_grant`-only update via a
    fresh `IServiceScopeFactory` scope, conditioned `WHERE sign_in_method =
    'microsoft-account'` so a concurrent disconnect is a no-op.
24. **Client ID plumbing**: key `Smtp:MicrosoftClientId`; GET additionally surfaces
    `microsoftSignInAvailable: boolean` (composed from key presence) so the client
    disables the choice with explanation before the click; begin still guards
    server-side with a 400 problem when unset.
25. **Sign-in Method row is progressive disclosure**: it *appears* only when the
    Outlook preset is selected — a permanently disabled radio for Gmail/Custom is
    dead UI; merging preset+method into one control was rejected (duplicates the
    preset list's job). Revisit with a saved microsoft account: host resolves to
    Outlook preset → row present, method pre-selected, Connected state shown.
26. **Client decomposition** (feature `src/features/email-settings/`):
    `useMicrosoftConnect.ts` composable owning begin/poll/reset and the union;
    `MicrosoftConnectPanel.vue` in `components/` taking composable state via typed
    props/emits; `api.ts` gains `beginMicrosoftConnect()` /
    `getMicrosoftConnectStatus()` + DTOs; the form composes the panel; the view stays
    thin. Folding the connect lifecycle into `useEmailSettings` was rejected — one
    composable per async lifecycle.
27. **Disconnect = the existing Remove, relabeled**: microsoft mode re-labels the
    existing Remove (DELETE) to "Disconnect…" — one destructive CTA, one endpoint;
    the confirm dialog names the config-file fallback consequence. A separate
    grant-clearing Disconnect endpoint was rejected (no sanctioned endpoint, and
    duplicate destructive intent on one form).

## Implementation grill — 2026-09-21 (round 4: grant race fix, migration, tests,
## per-state UX copy; all confirmed "all recommended")

28. **Migration** `Add_Smtp_Sign_In_Method_And_Grant`: `sign_in_method text NOT NULL
    DEFAULT 'app-password'` + check constraint `smtp_settings_sign_in_method_check IN
    ('app-password','microsoft-account')` (repo precedent: every enum column carries
    an `IN` check); `protected_grant text NULL`; `protected_password` → nullable.
    C#: `SignInMethod` enum on `SmtpSettingsRow` with `HasConversion<string>()`.
    `BuildSnapshotModel` re-added after scaffolding (repo gotcha stands).
29. **Store surface**: `ISmtpSettingsStore.SaveAsync` gains a discriminated payload —
    app-password → full upsert + clears grant; microsoft-account → From-fields-only
    update + clears password, grant untouched, credential columns derived from
    connect. Concrete-only `SmtpSettingsStore.ConnectMicrosoftAccountAsync(
    accountEmail, protectedGrantBlob)` for the Q12 one-upsert (Infrastructure-
    internal, off the public interface). Completeness per method in `LoadAsync`:
    microsoft = row present + grant blob *decrypts* (data-protection only — store
    stays MSAL-ignorant; a decryptable-but-corrupt blob surfaces later as
    `SignInExpired`).
30. **Grant-existence enforced at the handler**, after validation → 400 with copy
    "Connect a Microsoft account on this page before saving." Validators stay sync
    and pure (ADR-0004); method-conditional input rules land in
    `UpsertSmtpSettingsCommandValidator` `When` clauses mirroring client
    `validation.ts`.
31. **Test seam**: `ApiFactory` registers a hand-written `FakeMicrosoftAccountSignIn`
    by default (knobs: `BeginResult`, `SetPending/SetSucceeded(email)/SetFailed/
    SetExpired`, `TokenResult`/`TokenException`, call recording). NSubstitute
    rejected for the stateful begin→pending→terminal flow; `IEmailSender` fakes stay
    NSubstitute.
32. **Connect slices**: `Features/EmailSettings/BeginMicrosoftConnect/` +
    `GetMicrosoftConnectStatus/` with matching `Endpoints/EmailSettings/`. `POST
    /api/settings/smtp/microsoft-connect/begin` → `200 { userCode, verificationUrl,
    expiresAt }` (400 when Microsoft sign-in unavailable); `GET
    .../microsoft-connect/status` → `200 { state, accountEmail?, error? }` with
    lowercase state strings. Begin while in-flight replaces (settled); begin while
    already connected is allowed — that *is* the reconnect path.
33. **Connect panel states** (design-taste full state cycle; ADR-0008: restrained
    motion, inline feedback, no toasts): idle → "Sign in with the Microsoft account
    this app sends from." + primary **Connect Microsoft account** (disabled with
    helper when `microsoftSignInAvailable` is false); pending → "Enter this code at
    microsoft.com/link:" + code in Geist Mono with copy button (inline "Copied" swap,
    no toast) + "Waiting for sign-in — the code expires at HH:MM." + single CSS-pulse
    dot honoring `prefers-reduced-motion`; succeeded → "Connected as
    anna@outlook.com."; failed → inline server copy + **Try again** (re-begins);
    expired → "The code expired before sign-in finished." + **Get a new code**;
    restart-lost (poll returns `idle` while client believed `pending`) → "The sign-in
    attempt was lost when the server restarted — start again." + **Connect Microsoft
    account**. Polling: recursive `setTimeout` 2s (no VueUse in the client), cleared
    on terminal state and `onScopeDispose`; transient poll errors swallowed while
    pending server-side; no client timeout — server `expired` is authoritative.
34. **Switch-away confirm at the moment of the switch** (`UModal`): triggers = method
    radio microsoft→app-password while connected, or preset Outlook→Gmail/Custom
    while microsoft selected. Title "Switch to App Password?"; body "Saving after
    switching disconnects anna@outlook.com — the Microsoft sign-in is discarded when
    you save. Nothing changes until then."; buttons **Keep Microsoft account**
    (reverts) / **Switch to App Password**. No server call until Save; reverse
    direction needs no confirm.
35. **Save gating per method**: app-password unchanged (Save gated on
    `testState === 'passed'`); microsoft-account hides Test, enables Save when
    connected (`connectState === 'succeeded'` or `settings.signInMethod ===
    'microsoft-account'`) and From fields valid; editing only FromName under an
    existing connection keeps Save enabled. `format.ts` status line microsoft branch:
    "Sending as anna@outlook.com via Microsoft account — saved on this page."
36. **Ship-verbatim copy fixes** (decision 8): Outlook preset help → "Microsoft
    Account sign-in recommended — personal Outlook retired app passwords."; Gmail
    unchanged; appsettings Outlook comment → "Outlook: prefer the settings page's
    Microsoft Account sign-in. Basic auth here only fits M365 work/school accounts
    with SMTP AUTH enabled — Microsoft is retiring it." + commented
    `"MicrosoftClientId": ""` key with a one-line "public-client ID is not a secret"
    note.
37. **Client seam-spec inventory** (SettingsEmailView.spec.ts + two connect routes):
    (1) method radio appears only for Outlook preset; (2) method switch swaps
    credential fieldset ↔ connect panel; (3) pending→succeeded shows "Connected as…"
    and enables Save; (4) failed and expired render inline with retry buttons; (5)
    restart-lost copy; (6) Save gating per method; (7) switch-away confirm appears
    and reverts on cancel; (8) GET with `signInMethod: 'microsoft-account'` renders
    Connected state, From fields editable; (9) Outlook preset help copy. No
    composable unit tests — flow-first per ADR-0003.
38. **Docs**: no new CONTEXT.md glossary entries (the **Sign-in Method** entry
    already naturalizes "connected/disconnected"; Grant stays an implementation
    detail). ADR-0020 outline: context = basic-auth retirement for personal Outlook;
    decisions = provider scope, project public client + `common` tenant, MSAL over
    hand-rolled (with ADR-0012 package note for `Microsoft.Identity.Client` in
    Directory.Packages.props), discriminator schema + grant blob under its own
    protector purpose (with decision 16 naming), derived sender identity,
    connect-as-test, pre-flight refusal, settings-page-only + copy fixes;
    consequences = amends ADR-0019 point 2, adds the fake-sign-in test seam.
