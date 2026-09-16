# 01: Import a Google Forms CSV into an intake round

**Blocked by:** none

**Status:** ready-for-agent

**Terms:** Intake Source, Form Response, Resubmitted — [CONTEXT.md](../../../CONTEXT.md).
**Decision:** [ADR-0013](../../../docs/adr/0013-form-response-intake-with-ordinal-keyed-form-layout.md).

## What to build

Parallel to the existing `.eml` seam
(`src/hr-sat.Application/Features/Candidates/Import/`), a CSV import that creates one
candidate per form row, preserving the **raw row verbatim**.

- Accept a `.csv` upload into a vacancy's **active round** (reuses the round-scoped
  import authorization; lifecycle conflicts stay amber).
- **Hard layout gate (issue 02):** if the vacancy has no valid Form Layout (Name +
  Contact Email bound), the import is refused (409) and the client routes the first
  upload into the guided layout panel; the import completes there. Identity detection
  below applies to every import — all imports run under a valid layout, but identity is
  **never** derived from the layout's bound roles (roles are manual; identity is
  mechanical).
- Parse the Google Forms export (first row = headers; quoted multi-line cells are normal).
- Store on each candidate: an Intake Source discriminator (`form`), the **raw row's
  cells** (verbatim, ordered), the form Timestamp, and the detected identity key.
- **Identity key per row:** normalized email → digits-only phone → none.
- **Dedupe within the vacancy:** the same email key keeps only the *latest* row (form
  Timestamp wins) as its Form Response; earlier rows are retained as hidden prior
  submissions on that candidate.
- **Re-upload is additive:** new keys → new candidates; known keys → update the stored
  Form Response and set the **Resubmitted** flag; *never* touch review status, notes,
  requirement reviews, or typed details.
- Prior Application Notice: the form's detected **identity email** participates in the
  existing vacancy-wide email match (see settled decisions).

## Settled decisions (grill, 2026-09-16)

Backend:

- **Endpoint:** parallel route
  `POST /api/vacancies/{vacancyId}/rounds/{roundId}/candidates/import-form`, new slice
  `src/hr-sat.Application/Features/Candidates/ImportForm/`; the `.eml` path is untouched.
  Reuses `RoundWrite.ExecuteAsync` + `vacancy.EnsureCanReceiveCandidateImport` (vacancy
  open + round active), so lifecycle conflicts stay 409/amber.
- **Storage:** `Candidate` gains an `IntakeSource` discriminator (`email` | `form`). New
  table `CandidateFormResponses`: `CandidateId` FK, `Cells jsonb` (string[], verbatim,
  ordered), `FormTimestampRaw` string, `FormTimestampParsed timestamptz null`,
  `IdentityKey string null`, `IsCurrent bool`, `ImportedAt`; unique partial index on
  `(CandidateId) WHERE IsCurrent`. JSONB keeps ordinal addressing (`cells ->> n`) for
  issue 03's screening.
- **Identity detection (independent of the layout):** Timestamp = ordinal 0, always
  (Google Forms fixes the position; only the header text localizes). Email = first
  email-shaped cell (trimmed, lowercased). Phone fallback = first cell with ≥8 digits,
  only when no email-shaped cell exists. Else no key → always a new candidate. Identity
  is heuristic **by design** — a mechanical storage key never shown to HR and never
  derived from the manually bound roles — and is stamped at import and immutable; HR's
  later Candidate Details edits never rewrite it.
- **Dedupe ladder** (vacancy scope, lifecycle-respecting): (1) same email key twice in
  one CSV → latest form Timestamp wins, earlier row retained as hidden prior; (2)
  re-upload newer row → stored response replaced + Resubmitted, old current becomes a
  prior; (3) re-upload older row → skipped-outdated, no overwrite, no Resubmitted; (4)
  key known only in a closed round → new candidate in the active round + Prior
  Application Notice, settled data untouched; (5) no-key rows → always new. Timestamp tie
  or unparseable timestamp → later upload / later row in file wins; the raw string is
  always stored.
- **Prior Application Notice:** matches vacancy-wide over email candidates'
  `SourceSenderEmail` ∪ form candidates' stored identity email (immutable). Revisit at
  issue 02 once the bound Contact Email exists.
- **Response contract (200):** `{ rowsRead, created, updated, skippedOutdated,
  priorApplications }` — atomic, no per-row results. `updated` carries "(resubmitted)" in
  UI copy; `skippedOutdated` is deliberately distinct from the `.eml` flow's `skipped`
  (file-hash duplicate). Failures: 400 ValidationProblem (malformed CSV, naming the row
  position), 409 Problem (lifecycle conflict), 404 (vacancy/round).
- **Parser:** CsvHelper, added to `Directory.Packages.props` per ADR-0012; streaming
  read; all-or-nothing via the `RoundWrite` transaction; 25 MB cap (matches `.eml`).
  Malformed = unreadable structure, zero data rows, or a ragged row (cell count ≠ header
  count; blank trailing lines skipped). Dedupe loads the round's current form responses in
  one batched query keyed by `IdentityKey` — no N+1 (target: 5,000 rows < 10 s).

Client:

- **Placement:** new module `src/features/import-form/` (`api.ts`,
  `useFormResponseImport.ts`, `components/FormCsvDropZone.vue`); the import dialog becomes
  a shared shell under `src/features/import/` composing the existing eml dropzone (stays
  in `features/candidates/`) and the new csv dropzone. Cross-feature imports use root
  modules only.
- **One dialog:** the dropzone accepts `.eml` + `.csv`, dispatches by extension; mixed or
  wrong-type drops are rejected inline per file. Copy: "Drop .eml files or a Google Forms
  .csv export".
- **Busy state:** indeterminate only (spinner + "Importing…", controls disabled) — no
  fake progress bar, no streaming.
- **Result:** a summary line in the staying-open dialog — "214 rows read · 198 new · 12
  updated (resubmitted) · 4 skipped (outdated) · 9 prior applications noticed". Malformed
  → red inline alert naming the row position; closed round → amber alert with lifecycle
  re-expression copy; no valid layout → route into the guided layout panel (issue 02)
  instead of a plain error.
- **Resubmitted badge:** neutral `UBadge` stacked with the existing review-status badges
  in `CandidateList.vue` (reuses the established badge pattern; the review page itself is
  issue 04).

## Out of scope

Form Layout / column mapping (issue 02), screening (03), review-page rendering (04).

## Seam tests

Server (`tests/hr-sat.Tests/Candidates/`, handler-level, Testcontainers Postgres — JSONB
+ the partial index are Postgres features):

- Valid CSV → candidates created, `form` source, raw cells verbatim and ordered.
- Same email key twice in one file (differing timestamps) → one candidate, latest
  current, earlier retained as hidden prior.
- Re-upload fresher export → response updated + Resubmitted; review status, notes,
  requirement reviews, typed details untouched.
- Re-upload older rows → skipped-outdated; nothing overwritten; no Resubmitted.
- Identity fallbacks: phone-only → phone-keyed dedupe; empty email + empty phone → always
  new candidates.
- Key known only in a closed round → new candidate in the active round; the closed round
  untouched.
- Form identity email matches an existing email candidate's sender → counted in
  `priorApplications`.
- Closed round → 409 conflict; malformed CSV → 400 naming the row position; zero data
  rows → 400.
- Vacancy with no valid layout → 409 (hard gate); client routes the first upload into
  the guided layout panel.

Client (`VacancyDetailView.spec.ts` pattern — mount the view, stub fetch, assert visible
outcomes):

- Drop `.csv` → POST to `import-form` → summary line visible, toast, list refreshes.
- Drop `.eml` → existing flow unchanged (regression guard for the shared dialog).
- Mixed / wrong-type files → inline rejection, nothing uploaded.
- 409 → amber lifecycle alert; 400 → red alert naming the row position.
- Re-upload → Resubmitted badge on the affected candidate row.

## Acceptance

- [ ] `.csv` accepted into the active round; each row = one candidate with raw cells
- [ ] Raw row stored verbatim and never edited
- [ ] Email-key dedupe keeps latest, retains priors
- [ ] Re-upload additive; Resubmitted flag; review data untouched
- [ ] Prior Application Notice matches the form's stored identity email
- [ ] `import-form` endpoint + `ImportForm` slice; the `.eml` path untouched
- [ ] `CandidateFormResponses` storage: verbatim ordered cells + `IsCurrent` partial index
- [ ] Summary response `{ rowsRead, created, updated, skippedOutdated, priorApplications }`
- [ ] One import dialog accepting `.eml` + `.csv`, dispatch by extension
- [ ] Summary line + amber (conflict) / red (malformed) states; indeterminate busy state
- [ ] Resubmitted badge in the candidate list
- [ ] Backend + frontend seam tests pass (written after implementation)
