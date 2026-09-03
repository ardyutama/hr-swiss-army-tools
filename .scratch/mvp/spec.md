# MVP V1 Spec — Sorting CV

Iterations: **V1** = this spec (workspace + candidate tracking, no parsing). **V2** = hire
tracking for batch recruitment (`.scratch/v2/`). **V3** = CV text extraction and
requirement matching (`.scratch/v3/`, ADR-0009).

## Problem Statement

HR staff in a traditional company receive job applications as plain emails with PDF
attachments — no ATS. Today they open every email, check subjects, open each PDF one by one,
and take notes outside any system. The work is repeatable yet fully manual, CVs arrive in
mixed and design-heavy formats, and one inbox serves many different job requirements.

## Solution

A browser-based tool with one workspace per vacancy. HR creates a vacancy (title, date,
skills requirements), drops exported `.eml` files into it, and reviews the resulting
candidates in a side-by-side workspace: multi-page PDF viewer next to the vacancy's
requirements (a manual checklist) and the candidate's details. Each candidate gets a status
(new / flagged / shortlisted / rejected), notes, and contact closes the loop: per-vacancy
shortlisted/rejected email templates, reusable across vacancies, sent to all candidates at
once via `mailto:`.

## User Stories

From `docs/discovery/03-user-stories.md` — functional: US-1, US-2, US-3, US-4, US-5, US-9
through US-19. Non-functional: US-6, US-7, US-8.

## Implementation Decisions

### Process

- **Vertical slices**: one ticket = one narrow but complete feature path (schema, API, UI,
  tests). No horizontal layer tickets.
- **Work order inside a ticket**: backend slice → frontend feature folder → tests.
- **No TDD**: tests are written after the implementation within the same ticket, at the
  seams below. This is an explicit deviation from the default `/implement` behaviour.

### Backend — ASP.NET Core monolith, Vertical Slice Architecture + Clean Architecture layers

- One folder per feature (e.g. `Features/Vacancies/CreateVacancy.cs`): endpoint + handler +
  EF Core config live together in the slice.
- Clean Architecture layering across the solution: Domain entities, Application use-cases,
  Infrastructure (EF Core, file storage), API host. Slices draw from these layers; they
  don't re-create them.
- Single DbContext. Database per `docs/discovery/04-architecture.md` MVP diagram (Docker).

### Frontend — Vue SPA, feature-folder VSA

- `src/features/<feature>/` holds that feature's components, composables, and API client;
  `src/shared/` holds the thin shared kernel (layout, primitives, http client).

### Domain model (V1)

- **Vacancy**: title, date, status, skills requirements (list of lines). Progress is
  derived: candidates with a terminal review status / total candidates.
- **Candidate**: belongs to a vacancy; source email (subject, body, sender); candidate
  details (name, email, phone) start empty and are entered manually — there is no parser
  in V1; notes (free text); review status (`new` / `flagged` / `shortlisted` / `rejected`).
  Display name falls back to sender name, then sender email, then email subject until a
  name is typed.
- **CV document**: PDF file extracted from the `.eml` attachment, stored per candidate;
  multi-page, served for the viewer.
- **Email template**: per vacancy per kind (`shortlisted` / `rejected`); a template can be
  copied from a previous vacancy.

### Ingestion

- Import path is **`.eml` drag-and-drop** (screen S2). No live mailbox connection in V1.
- Each `.eml` yields one candidate: subject, body, sender, and PDF attachment(s).

### Review without parsing (V1)

- No PDF text extraction in V1: candidate details are typed by HR in the review workspace.
- Vacancy requirements display as a manual checklist next to the PDF; there is no computed
  match. Extraction and requirement matching are V3 (ADR-0009, `.scratch/v3/`).

### Contact / sending

- V1 has **no server-side SMTP**. "Send" renders the template with the candidate's data and
  opens a `mailto:` link / copies to clipboard; HR sends from their own email client.
- "Send To All" applies this to every candidate in the matching review status group.

### Hosting

- Deployed by the maintainer (Docker); HR users only open a browser (US-6, US-7).

## Testing Decisions

- Test **external behaviour only**, not implementation details.
- Backend seam: the **HTTP API** — one integration test class per slice, running against a
  real (test) database.
- Frontend seam: the **feature component** — component tests per feature folder.
- Tests are written after implementation, in the same ticket (no red-green-first cycle).

## Out of Scope

- PDF text extraction, candidate-skill extraction, and any computed match (V3, ADR-0009).
- Hire tracking: needed hires, hired/runaway/declined outcomes, shortage (V2,
  `.scratch/v2/`).
- Live mailbox integration (IMAP/Graph).
- Server-side email sending (SMTP) and delivery tracking.
- The "Maybe final" queue/worker architecture from `docs/discovery/04-architecture.md`.
- The two "No Idea" placeholder slots on the vacancy list screen.
- Auth / multi-user roles (single HR user assumption for V1).
- Ratings/scores.

## Further Notes

- The riskiest slice — PDF extraction from design-heavy CVs — moved out of V1 entirely
  (V3, gated by an OCR spike). V1's manual-entry path (ticket 08) stays load-bearing even
  after V3 lands: it is the failure UX for unparseable CVs.
- UI behaviour follows `docs/discovery/05-ui-sketches.md`.
