# Handoff — Issue 03: Microsoft Account sign-in (implementation, mid-flight)

**Date:** 2026-09-21 · **Branch:** `feat/implement-email-dispatch` · **Status:** backend slices 1–5 + 7 complete and green; client slice implemented; **client seam spec is 5/13 — the radio helper can't find URadioGroup items (single root cause)**; ADR-0020, review, and commit remain.

## Sources of truth — read these first

- Issue + all 38 settled grill decisions: `.scratch/send-to-all/issues/03-microsoft-account-sign-in.md` (the design contract — every behavior traces to a decision number)
- Prior issue's handoff (conventions, gotchas, env): `hr-sat-smtp-settings-handoff.md`
- Work order / no-TDD stance: `docs/agents/workflow.md`; test scope: `docs/agents/testing.md`
- Conventions: `docs/agents/dotnet.md`, `docs/agents/vue.md`, `.agents/skills/vertical-slice-dotnet/`, `.agents/skills/vue-feature-slices/`
- Full uncommitted change set: `git status` on the branch (everything listed there except `.DS_Store` / `.agents.zip` belongs to this issue)

## What was done

**Backend — slices 1–5 of the issue's decision-13 order, all green (79/79 in `EmailSettings|Dispatches` filter; `dotnet build hr-sat.slnx` clean):**

1. Migration `20260921123643_Add_Smtp_Sign_In_Method_And_Grant` — `sign_in_method text NOT NULL DEFAULT 'app-password'` + `IN` check constraint, `protected_grant text NULL` (decisions 16, 28). **The hand-maintained `BuildSnapshotModel` helper was re-added** to `AppDbContextModelSnapshot.cs` after scaffolding (repo gotcha, repo memory `ef-snapshot-helper.md`) and the scaffolded `defaultValue: ""` was hand-fixed to `'app-password'`.
2. MSAL seam: `IMicrosoftAccountSignIn` (+ `MicrosoftConnectChallenge`/`MicrosoftConnectStatus`/`MicrosoftConnectState`/`MicrosoftAccountToken`) in `Application/Abstractions/Email/`; `Infrastructure/Email/MicrosoftAccountSignIn.cs` — singleton `IPublicClientApplication`, device-code begin with in-memory attempt holder, stash-and-persist grant (decision 23: `AfterAccess` stashes + conditioned `protected_grant`-only update; connect completion upserts row+grant via concrete `SmtpSettingsStore.ConnectMicrosoftAccountAsync`), `idle|pending|succeeded|failed|expired` mapping with `authorization_declined`→failed / `expired_token`→expired. `Microsoft.Identity.Client` 4.90.0 added to `Directory.Packages.props` + Infrastructure.csproj per ADR-0012 (latest stable). Registered singleton in `Program.cs`.
3. Connect endpoints: `Features/EmailSettings/BeginMicrosoftConnect/` + `GetMicrosoftConnectStatus/` with `Endpoints/EmailSettings/BeginMicrosoftConnect/` + `GetMicrosoftConnectStatus/` — `POST /api/settings/smtp/microsoft-connect/begin` → 200 challenge / 400 when unavailable; `GET .../status` → lowercase states.
4. GET/PUT discriminators: `signInMethod` (`app-password|microsoft-account|null`) + `microsoftSignInAvailable` on both responses; `SmtpSettingsWrite` is now a discriminated union (`AppPassword` full upsert clears grant / `MicrosoftAccount` From-fields-only conditioned `ExecuteUpdateAsync` clears password); upsert validator is method-conditional (`When` clauses); handler does ignore-and-derive for microsoft + 400 `EmailSettings.MicrosoftSignInRequired` when no grant resolves.
5. Sender: `IEmailSender.IsConfigured` → `Task<SmtpReadiness> CheckReadinessAsync`; `MailKitEmailSender` switches on the `EffectiveSmtpConnection` abstract-record union (`AppPassword` → password auth; `MicrosoftAccount` → per-send `AcquireTokenAsync` + `SaslMechanismOAuth2`); both dispatch handlers pre-flight with `Dispatch.SmtpSignInExpired` amber refusal ("Reconnect the email account on the settings page.").

**Slice 7:** `appsettings.json` Outlook comment reworded (Microsoft Account sign-in first; basic auth = M365 work/school only) + commented `"MicrosoftClientId": ""` with the "public-client ID is not a secret" note (decision 36).

**Tests (backend):** `tests/hr-sat.Tests/FakeMicrosoftAccountSignIn.cs` (hand-written stateful fake per decision 31 — knobs `BeginResult`, `SetPending/SetSucceeded(email)/SetFailed/SetExpired`, `TokenResult/TokenException`, call counts; NSubstitute rejected for it); `ApiFactory` registers it by default (singleton, `Reset()` per `CreateClient`) and gains `SeedMicrosoftAccountAsync` (store's own one-upsert) + `ExecuteSqlAsync`; new `EmailSettings/MicrosoftSignInTests.cs` — 7 seam tests (begin challenge, begin-unavailable 400 + GET flag, status success/decline/expiry, connect→save→switch roundtrip proving both credential-discard directions, grant-required 400, unreadable-grant→file fallback via raw SQL, sender readiness through the grant + password path never touching the token seam); `EmailSettingsHandlerTests`/`ValidatorsTests` extended (signInMethod mapping, microsoft From-only save, refuse-without-grant, begin/status handlers, method-conditional validation); dispatch handler + HTTP-seam `Dispatch.SmtpSignInExpired` tests.

**Client — slice 6 implemented, `vue-tsc --build` clean, NOT yet test-green:**
- `api.ts`: `SignInMethod` type, `signInMethod`/`microsoftSignInAvailable` on `SmtpSettings`, `SmtpSettingsWritePayload` discriminated union, connect DTOs + `beginMicrosoftConnect()`/`getMicrosoftConnectStatus()`.
- `validation.ts`: `smtpFormSchema` is now a `z.discriminatedUnion('method', …)` (app-password branch = old rules; microsoft = From fields only); Outlook preset help → decision-36 copy.
- `useMicrosoftConnect.ts`: begin/poll/reset + union `idle|pending|succeeded|failed|expired|lost`; recursive `setTimeout` 2 s, cleared on terminal + `onScopeDispose`; transient poll errors swallowed while pending; `idle`-while-pending → `lost`.
- `components/MicrosoftConnectPanel.vue`: full decision-33 state cycle (copy verbatim), Geist Mono code + copy button (inline "Copied"), pulse dot gated on `prefers-reduced-motion: no-preference`. **`@fontsource/geist-mono` installed** + `--font-mono` token in `style.css`.
- `SmtpSettingsForm.vue`: Sign-in Method radio row only under Outlook preset (decision 25); credential fieldset ↔ connect panel swap; switch-away `UModal` (decision 34: method→app-password while connected; preset away from Outlook while microsoft selected; `pendingSwitchAway` applied only on confirm); Save gating per method (decision 35); Test button app-password-only; Remove relabeled "Disconnect…" under microsoft with revocation note; `applyServerErrors` now falls back to `problemDetailText` so `MicrosoftSignInRequired` surfaces inline.
- `SettingsEmailView.vue`: composes `useMicrosoftConnect`; succeeded → `load()` (connect is the test; server wrote the row); remove → `connect.reset()`.
- `SettingsEmailView.spec.ts`: 4 pre-existing tests updated (fixtures gain `signInMethod`/`microsoftSignInAvailable`; stub gains `begin`/`status` routes) + 9 new decision-37 tests.

## What's left

1. **Fix the spec's radio helper — 8/13 fail, one root cause.** `radioItem()` queries `[role="radio"]` and finds nothing (`no radio item containing 'Outlook'/'Gmail'/'App password'/'Microsoft account'`). A pre-check `grep -o 'role[^,]*' node_modules/reka-ui/dist/RadioGroup/RadioGroupItem.js` returned **no role string** — this Reka version does not put `role="radio"` on the item. Diagnose by dumping the rendered DOM: add a one-off `console.log(wrapper.html())` after mount in a scratch run, or inspect Nuxt UI v4's `node_modules/@nuxt/ui/dist/runtime/components/RadioGroup.vue` for the actual item markup. Likely fix: scope a plain-button query to the radio-group container (e.g. `[role="radiogroup"] button` by text) — beware text collisions: "Microsoft account" (method item) vs "Connect Microsoft account" (panel button) vs the Outlook help line; scope to the group element. The 5 passing tests (4 pre-existing + the saved-microsoft-account one) prove everything else works — form swap, gating, panel, modals — only radio interaction is unproven. Fake-timer discipline already applied per repo memory `client-testing.md` (native `.click()` inside the fake-timer window for late-rendered buttons; `advanceTimersByTimeAsync(2100)` + `flushPromises()`).
2. **Full gates:** `./node_modules/.bin/vitest run` (whole client suite), `npm run type-check`, `npm run lint`, then full `dotnet test` once at the end (per /implement).
3. **ADR-0020** (`docs/adr/0020-microsoft-account-sign-in-for-smtp-account.md`) — outline is settled in issue decision 38 (context = personal-Outlook basic-auth retirement; decisions = provider scope, project public client + `common` tenant, MSAL over hand-rolled + ADR-0012 note, discriminator schema + grant blob under purpose `smtp-microsoft-sign-in-v1` with decision-16 naming, derived sender identity, connect-as-test, pre-flight refusal, settings-page-only + copy fixes; consequences = amends ADR-0019 point 2, adds the fake-sign-in test seam).
4. **Review:** `/code-review` over the working tree + `ca-review` from the vertical-slice-dotnet pack (the dotnet guide's completion check).
5. **Commit** to `feat/implement-email-dispatch` — /implement mandates it; AGENTS.md reserves *pushing* for the user. Exclude `.DS_Store` and `.agents.zip`.
6. **Optional live smoke:** dev servers (API 5086 / Vite 5174) — the connect flow needs a real `Smtp:MicrosoftClientId`; without it the panel shows the disabled state + helper, which is itself worth a glance.

## Environment gotchas

- Client toolchain: `export PATH="$HOME/.nvm/versions/node/v24.15.0/bin:$PATH"`, then `./node_modules/.bin/vitest run <file>` from `src/hr-sat.Client/`. Never `npx vitest` (broken cache).
- `ApiFactory` ordering: the DB reset fires on the client's **first request** and `CreateClient()` resets the fake sign-in — seed rows / flip `IsAvailable` **after** the first request or after `CreateClient()`, respectively (both patterns are in `MicrosoftSignInTests.cs`).
- `SaslMechanismOAuth2(email, token)` and MSAL 4.90 APIs compiled clean on first pass — no API-shape surprises expected.
- Pre-existing (not ours): CS8602 warning in `ImportCandidatesTests.cs`; stray `.DS_Store`/`.agents.zip` at repo root.

## Suggested skills (next session)

1. **`vue-feature-slices`** (`references/testing.md`) and/or **`vue-testing-best-practices`** — fix the URadioGroup interaction in `SettingsEmailView.spec.ts`; the seam and assertions are right, only the item selector is wrong.
2. **`code-review`** (`.agents/skills/code-review/`) — mandated review pass over the diff (standards + the issue's 38 decisions).
3. **`ca-review`** from `.agents/skills/vertical-slice-dotnet/` — Clean-Architecture conventions check on the backend diff.
4. **`implement`** — to run the remaining gates, land ADR-0020, and commit.
