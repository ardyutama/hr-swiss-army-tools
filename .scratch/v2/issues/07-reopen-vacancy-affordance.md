# 07: Reopen affordance for closed vacancies

**What to build:** a client-side reopen path for closed vacancies. CONTEXT.md defines a
Closed Vacancy as "a read-only vacancy retained for reference after its hiring effort
ends; it can be reopened explicitly", and the backend already exposes
`POST /api/vacancies/{id}/reopen` → `VacancyDetailsResponse`
(`src/hr-sat.Web.Api/Endpoints/Vacancies/Reopen/Reopen.cs`). The client has no reopen
function anywhere: `features/vacancies/api.ts` exposes list/get/create/update/delete
only, and the existing reopen copy (`shared/problem-details.ts`, email-templates dialog)
points at an affordance that does not exist.

Scope:

- `reopenVacancy(id)` in `features/vacancies/api.ts` over `shared/http.ts`.
- Flow wiring (composable) + UI affordance for closed vacancies — vacancy detail header
  and/or the vacancy-list row actions (list redesign of 2026-09-15 deliberately shipped
  purge-only on closed rows; reopen was deferred as feature work, not presentation).
- Seam tests traced to `domain: closed vacancy is read-only` / reopen glossary power.

**Origin:** deferred from the vacancy-list redesign grill session (Q8ii), 2026-09-15 —
see repo memory `vacancy-list-redesign-2026-09-15.md`.

**Status:** needs-triage

- [ ] `reopenVacancy` API function
- [ ] Reopen flow + UI affordance on closed vacancies
- [ ] Seam tests
