# Handoff: Email templates per vacancy (MVP ticket 06) — design settled, ready to implement

**Project:** hr-swiss-army-tools (CV Sorting — ASP.NET Core + Vue 3 vertical slices)
**Workspace:** /Users/ardyputrautama/Documents/freelance-project/hr-swiss-army-tools
**Date:** 2026-09-13
**Session outcome:** design grilling for ticket 06 is COMPLETE. All decisions settled by the
user. Next session: implement the slice (schema → API → client → seam tests, in
docs/agents/workflow.md order).

## Read these first (in order)

1. `.scratch/mvp/issues/06-email-templates.md` — the settled spec. The "Design (settled
   2026-09-13)" section is authoritative: storage, endpoints, placeholders, render
   contract, copy-from semantics, frontend module shape, lifecycle rules, test plan.
2. `CONTEXT.md` — glossary. The **Email Template** entry was sharpened this session: two
   placeholders (candidate name, vacancy title); name fallback typed name → source sender
   name → neutral "there". Note "Avoid: Uploaded template file" — templates are subject +
   body text in the `email_template` table, NOT files (S5 sketch wording was stale).
3. `AGENTS.md` — architecture contract (vertical slices, shared code earned at 2nd
   consumer, no dead UI per ADR-0008).
4. `docs/agents/dotnet.md` and `docs/agents/vue.md` — repo-specific rules on top of the
   skill packs.

## Decisions captured (already written to the issue + CONTEXT.md — do not re-litigate)

- Vacancy-owned `EmailTemplate` child entity; mutations via `Vacancy` aggregate methods
  guarded by lifecycle rules, executed via the shared `VacancyWrite` locked-transaction
  helper (precedent: IntakeRounds slice).
- Endpoints under `/api/vacancies/{vacancyId}/email-templates`: `GET /` (both kinds),
  `PUT /{kind}` (upsert), `DELETE /{kind}`, `GET /sources?kind=`, `POST /render`.
- Rendering server-side: `POST /render` takes `{subject, body, candidateId}` → rendered
  `{subject, body}`; 404 if candidate not in vacancy. Case-insensitive placeholder
  matching, tolerant of inner whitespace; unknown tokens pass through literally.
- Copy-from = client-side GET + PUT (no dedicated endpoint). Sources = every OTHER vacancy
  owning that kind, closed included, newest `opened_on` first.
- Closed vacancy: template mutations refused as lifecycle conflict (409); View and
  copy-from remain legal.
- Frontend: self-contained `features/email-templates/` module (api.ts, useEmailTemplates,
  useTemplateSources, useTemplatePreview, validation.ts; components EmailTemplatesDialog,
  TemplateSection, TemplateEditorDialog with live draft preview, TemplateViewDialog with
  candidate picker fed the VIEWED round's candidates, inline delete confirm).
- VacancyDetailView: replace the disabled placeholder span (~line 256) with a working
  "Send Email to all candidate" button (label unchanged) opening the dialog. No Send To
  All — that is ticket 07. No ADR needed (decision was already in the confirmed DB design).

## Conventions map (verified by exploration this session)

- Backend slice exemplar: `src/hr-sat.Application/Features/Vacancies/` (per-operation
  folders: Command/Validator/Handler), entity split pattern in
  `src/hr-sat.Domain/Domain/Vacancies/` (Entity + *Errors + *Rules), EF config in
  `src/hr-sat.Infrastructure/Infrastructure/Configurations/` (auto-discovered), migrations
  via `dotnet ef` against hr-sat.Infrastructure. Scrutor auto-registers handlers and
  IEndpoint implementations; validators via FluentValidation assembly scan; validation
  decorator wraps command handlers.
- Errors: `Error.Conflict` → 409 (lifecycle), `ValidationError` → 400; mapping in
  `src/hr-sat.Web.Api/CustomResults.cs`. Server error keys are PascalCase; client
  lowercases via `fieldErrorsOf()` in `src/hr-sat.Client/src/shared/validation.ts`.
- DDL reference for `email_template` (subject ≤ 998, body non-empty, kind check,
  UNIQUE(vacancy_id, kind)): `docs/discovery/06-database-design.md`.
- Backend tests: flat folder `tests/hr-sat.Tests/EmailTemplates/`, ApiFactory +
  Testcontainers Postgres 18 (integration) and TestDbContext SQLite in-memory (unit).
- Frontend: `shared/http.ts` exports getJson/postJson/putJson/delJson throwing
  ApiError(status, problem). Seam tests stub `fetch` globally, assert dialogs via
  `document.body`, never assert dialog-closed (Transition timing), pattern in
  `src/hr-sat.Client/src/pages/vacancy-detail/VacancyDetailView.spec.ts`. Round IDs are
  JSON numbers; stringify only at route params.
- Run tests: client from `src/hr-sat.Client/` (Vitest); server
  `tests/hr-sat.Tests/hr-sat.Tests.csproj` (xUnit). Do NOT commit — user pushes.

## Open items / gotchas

- Ticket 07 (send via mailto) is the consumer of `POST /render` — keep its needs in mind
  but do NOT build send actions in 06.
- The vacancy-detail page's `useVacancyDetailFlow` already aggregates four lifecycles —
  do not add templates to it; the dialog drives its own composables.
- Known unrelated gap (flagged 2026-09-07): no close/new-round buttons on vacancy-detail.

## Suggested skills for the next agent

- **tdd** — if the user wants the slice built test-first (issue says tests are written
  after implementation; confirm preference).
- **vue-best-practices** (plus repo pack `.agents/skills/vue-feature-slices/`) — for the
  client module work.
- **postgres** — for the EF migration mirroring the confirmed `email_template` DDL.
- **code-review** — after implementation, to review the slice against the issue spec and
  repo standards.
- Read `.agents/skills/vertical-slice-dotnet/` pack (add-feature, add-entity, add-tests)
  per ADR-0005 before backend work — it owns the slice conventions.

## Redaction note

No secrets, credentials, or PII appeared in this session.
