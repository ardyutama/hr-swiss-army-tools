# Handoff — form-intake issue 02 (Form Layout config): design settled, implement next

**Date:** 2026-09-18
**Workspace:** repo root (`hr-swiss-army-tools`)
**Branch state:** uncommitted CONTEXT.md edits + issue-file edits from the grill session. Do NOT commit — the user pushes themselves (AGENTS.md commit rule).

## What happened

A grill session (grilling skill, 3 rounds, all recommended options confirmed by the
product owner) settled every open design decision for issue
[issues/02-form-layout-config.md](issues/02-form-layout-config.md)
(per-vacancy Form Layout: ordinal-keyed column mapping, roles, display columns, labels,
header drift, import gating, re-projection).

**Do not re-derive the decisions** — the settled option for every branch is recorded in
the issue file under **"## Grill decisions (2026-09-18)"** (19 numbered items covering
domain model, API seam, and client). That list is the authoritative output of that
session. The issue's Status line also records what code already exists vs. what 02 owes.

## Artifacts already updated (read these first)

- `.scratch/form-intake/issues/02-form-layout-config.md` — status `in-progress`, grill
  decisions log, acceptance list gained two items (typed Contact Phone; per-field
  provenance).
- `CONTEXT.md` — added **Picked Column** and **Form Answer** terms; amended **Form
  Layout** (re-projection never crosses a settled round) and **Header Drift** (confirm
  adopts the new snapshot). Glossary-only; no implementation detail belongs there.
- `.scratch/form-intake/issues/02-form-layout-config.layout.md` — pre-existing UI shape
  reference (panel modes, dialogs, slice map); decisions 17–19 build on it.
- `docs/adr/0013-form-response-intake-with-ordinal-keyed-form-layout.md` — the governing
  ADR; grill decisions refine it but do not contradict it. No new ADR was offered: the
  trade-offs extend ADR-0013's already-accepted direction.

## Where to start implementing

Work order per `docs/agents/workflow.md` (backend slice → client feature → seam tests;
implementation before tests per ADR-0003):

1. **Domain**: rewrite `FormLayout` (src/hr-sat.Domain/Domain/Vacancies/FormLayout.cs)
   around one `FormLayoutColumn` jsonb collection (decision 1); add
   `DetectDrift` + snapshot adoption (4); provenance enum + `PrefillDetailsFromLayout`
   + phone on `Candidate` (2, 5, 6, 7).
2. **Infrastructure**: migration replacing the four ordinal columns with the jsonb shape;
   three provenance columns on `candidate` (naming `PascalCase_With_Underscores`).
3. **Application/API**: `Error` extensions → `CustomResults.Problem` (9); flip
   `ImportFormCommandHandler` to parse-then-gate with layout payload + `confirmDrift`
   (10, 11, 14); `FormCsvParser` captures headers (10); `Upsert` drops client-sent
   snapshot, gains re-projection counts (12, 15).
4. **Client**: `src/features/form-layout/` module + `useFormResponseImport`
   `pendingRefusal` wiring on the vacancy-detail page (17–19), following the layout
   doc's slice map verbatim.
5. **Tests last**: backend handler/validator/domain tests per
   `tests/hr-sat.Tests/FormLayouts/` + `Candidates/` patterns; client seam tests on
   `VacancyDetailView` per ADR-0003 mechanics in `docs/agents/vue.md`.

Key existing code to mirror, not reinvent: `FormCsvParser` (CsvHelper, quoted multi-line
headers), `VacancyWrite.ExecuteAsync` (transaction helper), `Error`/`ValidationError`
(src/hr-sat.Domain/Domain/Error.cs), `CustomResults.Problem`, `useFormResponseImport`
(refusal interception point), `shared/problem-details.ts` (code-keyed messages).

## Suggested skills

- **vertical-slice-dotnet pack** (`.agents/skills/vertical-slice-dotnet/`: `add-feature`,
  `add-entity`, `add-tests`, `ca-review`) — the source of truth for every backend step.
- **vue-feature-slices** (`.agents/skills/vue-feature-slices/`) — source of truth for the
  client module (placement, state ownership, import discipline).
- **vue-best-practices** + **vueuse-functions** — during the SFC/composable work.
- **domain-modeling** — only if a new domain term surfaces during implementation;
  challenge it against CONTEXT.md before naming anything in code.
- Do NOT apply design-taste-frontend here: the layout doc already scoped it out (dense
  internal tool → ADR-0008 language, Nuxt UI v4, neutral drift dialog).

## Open threads (none blocking)

- Round-3 ratifications 1–8 (validation split, migration data sacrifice, flow ordering,
  response contracts) were accepted; if any ratification proves wrong in code, surface it
  rather than silently deviating.
- Issues 03 (screening rules) and 04 (review-page form variant) remain untouched and
  out of scope; 04 consumes the GET response shape decided in ratification 7.
