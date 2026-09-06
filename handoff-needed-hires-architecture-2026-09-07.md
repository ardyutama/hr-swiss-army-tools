# Handoff + Plan — Needed Hires architecture review

**Date:** 2026-09-07
**Repo:** `c:\Users\AU1833\Documents\personal\hr-swiss-army-tools` (hr-swiss-army-tools)
**Stack:** ASP.NET Core backend (vertical slices), Vue 3 + Nuxt UI v4 client
**State:** Work staged, NOT committed. AGENTS.md rule: never commit — the user pushes.

---

## Where we are

1. The user staged the full **V2 ticket 01: Needed Hires** slice
   (`.scratch/v2/issues/01-needed-hires.md`, all acceptance boxes ticked). Scope:
   nullable `NeededHires` (1–9999) on Vacancy end-to-end, interim `ActiveHires = 0`
   until ticket 02 (hire outcomes), "Filled" badge, round-close shortage
   confirmation, plus a ride-along refactor extracting `useVacancyDetailFlow`
   (53-member return) out of `VacancyDetailView.vue`.
2. I ran the **improve-codebase-architecture** skill over the staged diff.
   Output artifact: the candidate report (do not re-derive it):
   - `C:\Users\AU1833\AppData\Local\Temp\architecture-review-2026-09-07-needed-hires.html`
3. **Awaiting the user's pick.** The skill's next step is the grilling loop on the
   chosen candidate. Nothing has been implemented from the review yet.

## The four candidates (summary — full cards/diagrams in the report)

| # | Candidate | Strength |
|---|-----------|----------|
| 1 | One module for **Shortage**/**Filled** derivation — arithmetic is copied 3× (`format.ts`, `HiringPlanSummary.vue`, `useVacancyDetailFlow.ts`), display copy typed 2× with drift ("10 to go" vs "3 still needed") | **Strong** — top recommendation |
| 2 | `VacancyFormDialog` owns its saved baseline — replace `savedPayload` prop + watcher with a `markSaved()` expose (symmetric with existing `applyServerErrors()`); deletes the deep-copy dance in `VacancyListView.vue` | **Strong** |
| 3 | Deepen `useVacancyDetailFlow`'s 53-member flat interface — ~16 members are dialog open/request/dismiss/confirm boilerplate absorbable by `v-model:open`; note: flow has exactly one caller (hypothetical seam) | Worth exploring |
| 4 | Split `VacancyDetailsResponse.From()` into two named constructions (new vs stored vacancy) so the interim-zeros hiring fallback can't silently survive ticket 02; delete now-dead `VacancyProgress.GetAsync` (verified 0 callers) | Worth exploring |

## Plan for the next session

1. Ask the user which candidate to take (they hadn't answered). My
   recommendation: **candidate 1 first** — smallest change, highest locality, and
   ticket 02 lands on exactly that seam next.
2. Run the **grilling loop** on the pick (see suggested skills): constraints,
   shape of the deepened module, what survives as tests.
3. Implement behind the repo's conventions (below), then validate:
   - Client: `npx vitest run` from `src/hr-sat.Client/` (+ `npx vue-tsc --noEmit`
     if types touched). Lint: oxlint only (ADR-0001).
   - Server: `dotnet test tests/hr-sat.Tests/hr-sat.Tests.csproj`.
4. Leave changes staged-or-working for the user to commit themselves.

### Implementation sketch per candidate (if picked)

- **1 (hiring module):** create `src/hr-sat.Client/src/features/vacancies/hiring.ts`
  exporting `hiringShortage`, `isFilled`, `hiringProgressText` over
  `VacancyHiring`; re-point `HiringPlanSummary.vue`, `VacancyTable.vue`,
  `useVacancyDetailFlow.ts` (`roundShortage`), unify list-row vs workspace-strip
  copy; add pure-function tests in `hiring.spec.ts` (repo has a test-backfill gap
  noted in `.github/copilot-instructions.md`, but a NEW module should ship tested).
- **2 (dialog baseline):** delete `savedPayload` prop + watcher from
  `VacancyFormDialog.vue`; add `markSaved()` to `defineExpose` (baseline =
  `currentFormPayload()`); `VacancyListView.vue` drops `lastSavedPayload` and the
  deep copy; update `VacancyListView.spec.ts` ("US-11: HR sees when an edited
  hiring target is saved" test still passes via `applyServerErrors`-style ref).
- **3 (flow interface):** push open-state into the four dialogs
  (`v-model:open`), delete setter pass-throughs (`openImport`/`closeImport`),
  group remaining returns by render region. Do only with user buy-in — the flow
  was just extracted this same session; churn risk is real.
- **4 (projection paths):** split `VacancyDetailsResponse.From` into
  `FromNewVacancy` / `FromStoredVacancy(progress, rounds, hiring)` (required
  params); delete `VacancyProgress.GetAsync`; consider renaming `VacancyProgress`
  module. Verify no test regressed: `CreateVacancyTests`, `ListVacanciesTests`,
  `UpdateVacancyTests`.

## Suggested skills (call the Skill tool for these)

- **grilling** — required next step per improve-codebase-architecture; walk the
  picked candidate's decision tree with the user.
- **codebase-design** — the vocabulary (module/interface/depth/seam/adapter/
  leverage/locality) all suggestions must use; also its design-it-twice pattern
  if the user wants alternative interfaces.
- **domain-modeling** — candidate 1 names a module after domain terms (Shortage,
  Filled); keep `CONTEXT.md` current if terms sharpen. If the user rejects a
  candidate with a load-bearing reason, offer an ADR.
- **tdd** — repo convention is flow-first seam tests (ADR-0003,
  `docs/agents/testing.md`); write tests with the implementation.

## Repo conventions that constrain the work

- `AGENTS.md`: vertical slices; features never import sibling-feature internals
  (root modules only); shared code earned at second same-reason consumer;
  **never commit** — user pushes.
- ADR-0007: Nuxt UI v4 is the UI source of truth; no `App*` wrappers; `UApp` at
  root. Relevant to candidates 2–3 (dialog open-state via `v-model:open`).
- ADR-0010: intake rounds carry no quota; Needed Hires/Shortage are
  vacancy-level rollups. Do not propose per-round quotas.
- ADR-0003 + `docs/agents/testing.md`: seam tests — client specs mount the view
  and stub `fetch`; server tests are xUnit HTTP-seam tests.
- Client feature docs: `docs/agents/vue.md`; backend: `docs/agents/dotnet.md`.
- Domain language: `CONTEXT.md` — Shortage, Filled, Needed Hires, Active Round
  are defined terms; use them exactly in code and discussion.

## Context for continuation

- Full staged diff captured during review (may be stale if user has since
  staged more): `C:\Users\AU1833\AppData\Local\Temp\staged-diff.txt`.
- `useCandidateFilter` was also extended this session with a `CandidateListState`
  union (`loading | error | empty(reason) | ready`) taking an explicit
  `CandidateListContext` — that pattern was judged good; don't re-litigate it.
- No sensitive data involved; nothing redacted.
