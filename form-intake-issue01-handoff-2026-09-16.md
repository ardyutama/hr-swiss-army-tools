# Handoff — form-intake issue 01 implementation (2026-09-16)

**For:** a fresh agent implementing the CSV form-response import slice.
**Session focus:** implementing
`.scratch/form-intake/issues/01-csv-form-response-import.md` per the decisions the user
confirmed in a 3-round grill session on 2026-09-16 ("yes to all recommended" ×3).

## Read first (in order)

1. `HANDOFF.md` (repo root) — project onboarding.
2. `.scratch/form-intake/spec.md` — feature shape and non-goals.
3. `.scratch/form-intake/issues/01-csv-form-response-import.md` — the work item. It now
   carries a **"Settled decisions (grill, 2026-09-16)"** section: endpoint shape, storage
   schema, identity heuristics, dedupe ladder, response contract, parser choice, client
   placement, UX states, plus expanded server/client seam-test lists and an updated
   acceptance checklist. **Do not re-derive any of it.**
4. `CONTEXT.md` — canonical glossary. New this session: **Column Label** (HR's
   display-only short name for a picked form column; ordinal-bound, HR-written only,
   discarded on un-pick, never Settles). Also fixed: the Screening Rule operator list
   (duplicate `not-equals` → `not-empty`).
5. `docs/adr/0013` + `docs/adr/0014` — intake/layout and screening decisions.
6. `.scratch/form-intake/issues/02-form-layout-config.md` — amended for Column Label
   (terms line, semantics bullet, seam test, 3 acceptance criteria). Not this slice, but
   issue 01's storage must not foreclose it.

## What lives only here (not in other artifacts)

Everything else from the grill was folded into the files above. The following context was
deliberately **not** written into repo artifacts:

- **Design-read rationale for the client work.** The `design-taste-frontend` skill
  explicitly disclaims internal data-entry product UI; this repo's design law is ADR-0008
  + Nuxt UI v4 direct (no `App*` wrappers). If you consult the skill, use its trust-first
  dial row (VARIANCE 3 / MOTION 2 / DENSITY 5) and borrow only: full state cycles
  (loading/empty/error/tactile), copy self-audit, one CTA per intent, WCAG contrast
  checks. Landing-page aesthetics do not apply.
- **Codebase facts gathered by exploration** (verify before relying, code may drift):
  - `.eml` seam: `src/hr-sat.Application/Features/Candidates/Import/` —
    `ImportCandidatesCommandHandler` uses `RoundWrite.ExecuteAsync` +
    `vacancy.EnsureCanReceiveCandidateImport(roundId)`; endpoint
    `POST /api/vacancies/{id}/rounds/{id}/candidates/import` (multipart, antiforgery
    disabled); per-file results DTO in `ImportContracts.cs`.
  - Client import flow: `src/features/candidates/` — `ImportCandidatesDialog.vue` →
    `ImportDropZone.vue` (eml-only), `useCandidateImport.ts`, `api.ts`; results stay in
    the dialog; toast on completion.
  - `CandidateList.vue` already stacks `UBadge`s (review status, hire outcome, no-CV
    warning) — the Resubmitted badge reuses that pattern.
  - Client seam-test pattern: `src/pages/vacancy-detail/VacancyDetailView.spec.ts` —
    mount view, `stubFetch` by URL, simulate drop, assert text + toast spy.
  - No CSV library is currently in `Directory.Packages.props`; CsvHelper is a new pinned
    dependency per ADR-0012.

## Conventions that bite (this repo)

- Vertical slice; tests written **after** implementation, same ticket (no TDD).
- Seam tests: server = handler-level with Testcontainers Postgres (JSONB + the partial
  index need real PG); client = mount the view, stub fetch, assert user-visible outcomes.
- Glossary is law — use only the settled copy vocabulary ("updated (resubmitted)",
  "skipped (outdated)"); amber for Lifecycle Conflicts, red for validation/technical
  failures.
- Never commit; the owner pushes.

## Suggested skills

Call the Skill tool for:

- `implement` (repo-local, `.agents/skills/implement/`) — this repo's implementation
  workflow skill; present in the repo, not exercised this session.
- `vertical-slice-dotnet` pack (`add-feature`, `add-tests`) — backend slice conventions,
  source of truth per ADR-0005.
- `vue-feature-slices` — client slice conventions, source of truth per ADR-0011.
- `domain-modeling` — if any new term appears or a settled term needs sharpening.
- `design-taste-frontend` — only as constrained above (trust-first dials).
- `grilling` — if you hit an unsettled fork, stress-test it before coding.
- `code-review` — after the slice lands, review against the issue's acceptance list.

## Sensitive information

None handled this session; nothing to redact.
