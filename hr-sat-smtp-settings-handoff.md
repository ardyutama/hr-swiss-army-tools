# Handoff — Issue 02: SMTP settings page (implementation)

**Date:** 2026-09-20/21 · **Branch:** `feat/implement-email-dispatch` · **Status:** implementation complete and green; review + commit remain.

## Sources of truth — read these first

- Issue + all 16 settled grill decisions: `.scratch/send-to-all/issues/02-smtp-settings.md`
- UI shape: `.scratch/send-to-all/issues/02-smtp-settings.layout.md`
- Work order / no-TDD stance: `docs/agents/workflow.md` (tests after implementation, per ticket)
- Conventions: `docs/agents/dotnet.md`, `docs/agents/vue.md`, `docs/agents/testing.md` (traceability filter)

## What was done

Full vertical slice per the issue's decision-16 order. Everything is **uncommitted** on `feat/implement-email-dispatch` (a `git status` shows the full file set; nothing else was branched off).

**Backend**
- Migration `20260920155527_Add_Smtp_Settings_And_Dispatch_From_Address` — `smtp_settings` singleton table (id=1 check constraint, `protected_password` column) + `dispatch.from_address`. The hand-maintained `BuildSnapshotModel` helper in `AppDbContextModelSnapshot.cs` was re-added after scaffolding (repo memory `ef-snapshot-helper.md` explains).
- `Domain/Dispatches/Dispatch.cs` — `FromAddress` on the snapshot; factories/`MarkSent`/`MarkFailed` take it.
- `Domain/EmailSettings/EmailSettingsErrors.cs` — `EmailSettings.TestFailed` (sanitized, stage-tagged) + `EmailSettings.Invalid`.
- `Application/Abstractions/Email/` — `ISmtpSettingsStore`, `ISmtpConnectionTester`, `SmtpSettingsSnapshot`, `SmtpSettingsWrite`, `SmtpSettingsSource` (+`ToApiValue`), `SmtpTestRequest`, `SmtpTestResult`. Handlers never see ciphertext or configuration.
- `Features/EmailSettings/{GetSmtpSettings,TestSmtpConnection,UpsertSmtpSettings,RemoveSmtpSettings}/` + `Endpoints/EmailSettings/*` — `GET/PUT/DELETE /api/settings/smtp`, `POST /api/settings/smtp/test`; `Tags.EmailSettings` added.
- Infrastructure: `SmtpSettingsStore` (EF row + Data Protection purpose `smtp-settings-v1`, complete-row-wins/file-fallback, per-scope pinned resolution, undecryptable password → treated as incomplete), `MailKitSmtpConnectionTester` (connect+auth only, ~15s, sanitized messages), `MailKitEmailSender` reworked to scoped store-backed (**`IEmailSender` contract unchanged**, per decision 2).
- `Program.cs`: `AddDataProtection()` + scoped registrations; `Microsoft.AspNetCore.DataProtection` 10.0.12 added train-aligned per ADR-0012.
- Both dispatch handlers resolve the account once per run (decision 13) and stamp `from_address`.

**Client** (`src/hr-sat.Client/`)
- `features/email-settings/` — `api.ts`, `validation.ts` (Zod, preset-aware username rule), `format.ts` (status-line copy, `problemDetailText`), `useEmailSettings.ts` (view-state union, no toasts — decision 10), `components/SmtpStatusLine.vue`, `components/SmtpSettingsForm.vue` (URadioGroup preset cards, write-only password, test-gated Save, remove-confirm modal).
- `pages/settings/SettingsEmailView.vue` (+ seam spec) — view catches ApiError and delegates to the form's exposes (VacancyListView idiom).
- Route `/settings/email` in `router.ts`; sidebar "Settings" group + "Email sending" item in `App.vue`.

**Tests** — all traced `domain: SMTP Account`
- Backend: `tests/hr-sat.Tests/EmailSettings/` (5 HTTP-seam integration incl. sanitized-failure + stored-password-fallback probes against `localhost:1`; 6 handler unit; 8 validator). `ApiFactory` now installs a file-based `Smtp` section (host `smtp.test.invalid`) so dispatch rows record a `from_address` with the mocked sender, and truncates `smtp_settings` on reset. Dispatch unit-test helpers take the new store substitute.
- Client: `pages/settings/SettingsEmailView.spec.ts` — 4 seam tests (none-state, test-gate+save with status-line update, failed test stays locked, remove falls back to file).

## Verification so far

- `dotnet build hr-sat.slnx` — clean (one pre-existing warning in `ImportCandidatesTests.cs`, not ours).
- Backend full suite: **394/394 green**. Client full suite: **238/238 green**; `npm run type-check` + `npm run lint` clean.
- Live smoke (in progress): page renders per layout doc at `http://localhost:5174/settings/email` — presets, gmail prefill, "Email sending is not configured." status line, Save disabled. Username/password/from-address were typed into the live form; **the Test-connection click had not been made yet** when the session paused.

## What's left

1. **Finish the live smoke:** click "Test connection" on the open page (browser page `http://localhost:5174/settings/email`; dev servers running: API on 5086, Vite on 5174). Expect the sanitized failure inline ("Authentication failed — check the app password." if Gmail is reachable, else "Could not connect to smtp.gmail.com:587.") and Save to stay locked. Optionally switch to the Custom preset and confirm host/port clear. Then stop both dev servers.
2. **Review:** run the repo's review pass per the /implement skill — the `code-review` skill on the working tree, plus `ca-review` from the `vertical-slice-dotnet` pack (the dotnet guide's completion check).
3. **Commit** to `feat/implement-email-dispatch` — the /implement skill mandates committing; AGENTS.md reserves *pushing* for the user. Do NOT commit `.DS_Store`; `src/hr-sat.Client/components.d.ts` was auto-regenerated by the dev server (tracked file, registrations for the new components — include it). Note: `.scratch/send-to-all/issues/02-smtp-settings.md`, `CONTEXT.md`, and the layout doc were already modified/untracked from the grill session before implementation — they're part of this issue's change set.

## Out of scope (deliberately)

- The amber refusal banner's "Set up email sending" link — waits on issue 01's client slice (`features/dispatches/` doesn't exist); decision 14 assigns the link to whichever lands second.

## Environment gotchas

- Client toolchain: `export PATH="$HOME/.nvm/versions/node/v24.15.0/bin:$PATH"`, then `./node_modules/.bin/vitest run <file>` from `src/hr-sat.Client/`. Never `npx vitest` (broken cache). See repo memory `client-toolchain.md`.
- A **stale `dotnet run` process was squatting on port 5086** with an old binary (404'd the new endpoints); it was killed this session. If the smoke page 404s again, check `lsof -nP -iTCP:5086 -sTCP:LISTEN`.
- Anomaly (not root-caused, resolved): the very first browser render showed live settings data matching the *test* factory's `smtp.test.invalid` values even though the only listener on 5086 was the stale old-binary process. After the restart, the page and `curl` agree on the correct `source: "none"`. If it recurs, suspect a second Vite/API pair from an earlier session (Vite 5173 was also occupied).
- EF scaffolding drops the snapshot's `BuildSnapshotModel` helper — re-add after any future `dotnet ef migrations add` (repo memory `ef-snapshot-helper.md`).
- Client-spec lessons added this session to repo memory `client-testing.md`: `formRef.submit()` button idiom (no native submit clicks in jsdom), label→`for`→`#id` field querying, `CSS.escape` unavailable, URadioGroup handler param is `unknown`-safe.

## Suggested skills (next session)

1. **`/code-review`** (`.agents/skills/code-review/`) — review the working tree against standards + the issue's decisions; the mandated next step.
2. **`ca-review`** from `.agents/skills/vertical-slice-dotnet/` — Clean-Architecture conventions check over the diff.
3. **`vue-feature-slices`** (`.agents/skills/vue-feature-slices/`) — only if continuing to the decision-14 banner link or 01's `features/dispatches/` client slice.
4. **`grilling`** — only if a design decision needs stress-testing (e.g. if review disputes the sync-over-async `IEmailSender.IsConfigured` seam or the singleton-row-with-fallback store shape).
