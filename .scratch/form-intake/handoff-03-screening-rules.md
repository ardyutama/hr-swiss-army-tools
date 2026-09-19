# Handoff: Issue 03 — Screening Rules (design settled, ready to implement)

Date: 2026-09-19

## Where things stand

A three-round grill session (grilling + domain-modeling skills) for issue 03 is
**complete**. The product owner accepted every recommended option (Q1–Q20). All design
decisions are recorded in the issue file — **read it first; it is the spec**. No
implementation has started.

Artifacts produced/updated by the session (uncommitted — the user commits themselves
per AGENTS.md):

- `.scratch/form-intake/issues/03-screening-rules.md` — full "Grill decisions
  (2026-09-19)" section (20 numbered decisions), expanded seam tests + acceptance.
- `.scratch/form-intake/issues/03-screening-rules.layout.md` — pinned UI shape
  (editor modal, toolbar toggle, chips, review banner, promote chip).
- `CONTEXT.md` — Vacancy Progress entry amended: screened-out candidates are excluded
  from the denominator.
- `docs/adr/0014-…md` — amended: freeze-at-closure is a stored per-candidate verdict
  written once by `CloseRound`; evaluation is a pure domain function; read-path
  framing updated.

## Read first (in order)

1. `AGENTS.md` (repo rules — incl. "don't commit"), `CONTEXT.md` (glossary).
2. `.scratch/form-intake/issues/03-screening-rules.md` + `03-screening-rules.layout.md`.
3. `docs/adr/0014-screening-rules-as-computed-disposition-never-deletion.md` (amended),
   `docs/adr/0013-form-response-intake-with-ordinal-keyed-form-layout.md`,
   `docs/adr/0010-intake-rounds-as-candidate-owning-unit.md`.
4. `docs/agents/workflow.md` (slice order), `docs/agents/dotnet.md`,
   `docs/agents/vue.md`, `docs/agents/testing.md`.

## Watch out: dependency state (issue 02)

Issue 03 is blocked by `02-form-layout-config`, whose status line says "implementation
not started" — **but the code disagrees**: the unified `FormLayout.Columns` shape
(`FormLayoutColumn(Ordinal, Role?, Label?)`) and a migration named
`…_FormLayoutColumns_And_CandidateProvenance` already exist, and the vacancy detail view
already mounts `FormLayoutDialog` / `FormLayoutSummary`. Verify issue 02's true state
(git log + its acceptance checklist) before starting 03; its status line is stale.

## Code landmarks (facts gathered during the session)

- Raw rows: `CandidateFormResponse.Cells` is `string[]` as jsonb
  (`src/hr-sat.Domain/Domain/Candidates/CandidateFormResponse.cs`; EF config beside it
  in `src/hr-sat.Infrastructure/Infrastructure/Configurations/`). `IsCurrent` marks the
  latest response per identity.
- Storage precedent: `FormLayout` — one row per vacancy, jsonb payload, GET/PUT at
  `/api/vacancies/{id}/form-layout` (`src/hr-sat.Web.Api/Endpoints/FormLayouts/`).
  Screening-rule storage mirrors this (decision 1).
- List today: `ListCandidatesQueryHandler`
  (`src/hr-sat.Application/Features/Candidates/List/`) is fetch-all, ordered by
  ImportedAt then Id; `CandidateSummaryResponse` is a 13-field record.
- Client list consumers (all must survive the contract split, decision 10):
  `src/hr-sat.Client/src/features/candidates/api.ts` (`listCandidates`),
  `features/candidates/useCandidateFilter.ts` + `filter.ts` (client-side status /
  outcome / query / sort + route-query serialization — filtering dies, URL sync stays,
  decision 15), `features/review/useReview.ts` (review queue, `loadContext`),
  `features/promote-candidates/usePromoteCandidates.ts`.
- Delete path: `DeleteCandidateCommandHandler` routes through
  `Features/Shared/RoundWrite.ExecuteAsync` + `vacancy.EnsureCanRemoveCandidate` —
  closed-round candidates are already undeletable; the screening guard (decision 7)
  only adds a 409 for the active round.
- Round state: `IntakeRound.IsOpen` (`ClosedAt is null`).
- Errors: `CustomResults.Problem` maps Validation→400, NotFound→404, Conflict→409.
- Tests: `tests/hr-sat.Tests/` xUnit; `ApiFactory` (Testcontainers Postgres,
  TRUNCATE between tests), `TestDbContext`, `Candidates/CandidateTestData.cs` helpers;
  handler-level tests e.g. `ListCandidatesHandlerTests.cs`.
- Migrations: `src/hr-sat.Infrastructure/Migrations/`, `YYYYMMDDHHMMSS_PascalCase.cs`.

## Suggested skills

Call the Skill tool for:

- **postgres** — jsonb querying/translation for the paged list, the additive migration,
  pagination performance.
- **vue-best-practices** (mandatory for any Vue work) and **vue** — the client slices.
- **vueuse-functions** — debounced search input and preview-call debounce.
- **vue-testing-best-practices** — the Vitest view seams.
- **domain-modeling** — if any term sharpens during implementation, update CONTEXT.md
  inline.

Also read (repo-mandated skill packs, not Skill-tool skills):
`.agents/skills/vertical-slice-dotnet/` (`add-feature`, `add-entity`, `add-tests`,
`ca-review`) and `.agents/skills/vue-feature-slices/` — the per-ADR sources of truth
for slice conventions.

## Constraints to respect

- Don't commit; the user pushes themselves.
- Seam tests are written **after** implementation (issue's own acceptance line;
  flow-first testing per ADR-0003 / docs/agents/testing.md).
- Nuxt UI v4 directly + Tailwind v4; no `App*` wrappers, no `src/shared/ui/` (ADR-0007).
- Amber is reserved for Flagged / Lifecycle Conflict — screened-out surfaces are
  neutral (single exception: the per-rule all/nothing warning in the editor).
- Execution order per decision 20: backend slices (schema → rule-set CRUD →
  evaluation + freeze → paged list → delete guard → preview), then client (screening
  feature module → list rewrite → editor).
