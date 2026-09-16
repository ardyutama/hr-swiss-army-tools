# 06: Email templates per vacancy

**What to build:** The bulk-email dialog (S5) minus sending: for a vacancy, HR manages a
Shortlisted template and a Rejected template (editable subject + body text in the
`email_template` table, one per kind per vacancy), with View/Delete, and can copy a
template from a previous vacancy via the "Previous Template" pickers. Templates render with
the candidate's data (name, vacancy title) as placeholders.

**Blocked by:** 05-review-workspace

**Status:** ready-for-agent

- [X] Create/replace a Shortlisted template for a vacancy; View and Delete work
- [X] Create/replace a Rejected template for a vacancy; View and Delete work
- [X] Copy a template from a previous vacancy (independent copy)
- [X] Template rendering substitutes candidate name and vacancy title (server-side, previewable)
- [X] Closed vacancy: template mutations refused as lifecycle conflict; View and copy-from still work
- [X] Backend and frontend tests pass (written after implementation)

## Design (settled 2026-09-13, grilling session)

- **Storage:** text-based per CONTEXT.md ("Email Template") and
  `docs/discovery/06-database-design.md` — subject ≤ 998 chars, body non-empty,
  kind ∈ {shortlisted, rejected}, `UNIQUE(vacancy_id, kind)`. S5's "File Template" wording
  was stale sketch language; no files, no sweeper involvement.
- **Backend slice:** `EmailTemplate` is a Vacancy child entity; mutations go through
  `Vacancy` aggregate methods guarded by lifecycle rules, executed via `VacancyWrite`
  (locked transaction). Endpoints under `/api/vacancies/{vacancyId}/email-templates`:
  `GET /` (both kinds), `PUT /{kind}` (upsert), `DELETE /{kind}`, `GET /sources?kind=`,
  `POST /render`. Feature folder `Features/EmailTemplates/`, entity under
  `Domain/EmailTemplates/`, EF config mirroring the confirmed DDL.
- **Placeholders:** exactly `{{candidate_name}}` and `{{vacancy_title}}`; matching is
  case-insensitive and tolerant of inner whitespace; unknown tokens render literally. Name
  fallback: typed name → source sender name → neutral "there" (glossary, updated).
- **Rendering:** server-side. `POST /render` takes `{subject, body, candidateId}` (explicit
  text, so unsaved drafts preview too) and returns rendered `{subject, body}`; the
  candidate must belong to the vacancy (404 otherwise). Query handler, no transaction.
- **Copy-from:** no dedicated endpoint — the client reads the source template via `GET` and
  saves via `PUT`; independence is by construction. Sources list: every other vacancy
  owning that kind, closed included, newest `opened_on` first.
- **Candidate scope:** the preview picker lists the viewed round's candidates; templates
  stay vacancy-scoped so one pair serves every round.
- **Frontend:** `features/email-templates/` module — `api.ts`, `useEmailTemplates.ts`,
  `useTemplateSources.ts`, `useTemplatePreview.ts`, `validation.ts`; components
  `EmailTemplatesDialog` (S5 shell), `TemplateSection`, `TemplateEditorDialog` (subject +
  body + placeholder legend + live draft preview), `TemplateViewDialog` (read-only text +
  candidate picker + rendered preview), inline delete confirm. Dialog is self-contained
  (own composables; props: vacancyId, status, viewed-round candidates).
- **Vacancy-detail wiring:** the disabled "Send Email to all candidate" placeholder span
  becomes a working button (label unchanged) that opens the dialog. Closed vacancy: dialog
  opens read-only with an amber alert; mutating buttons hidden.
- **No Send To All** in this ticket — it arrives in 07-send-mailto; no dead UI (ADR-0008).
- **Tests:** backend `tests/hr-sat.Tests/EmailTemplates/` — upsert creates then replaces,
  validation 400s, delete, sources rules (excludes current, includes closed, order), render
  matrix (both placeholders, fallback chain, unknown tokens, cross-vacancy candidate 404),
  closed-vacancy upsert/delete → 409. Frontend seam tests in `VacancyDetailView.spec.ts` —
  button opens dialog, create asserts PUT body, View asserts render POST carries
  candidateId, copy-from flow, closed vacancy read-only. Traced to US-19.
