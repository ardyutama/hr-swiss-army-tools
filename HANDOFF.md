# Handoff — hr-swiss-army-tools

**For:** a fresh agent or engineer picking up this repo cold.
**Read order:** this file → `AGENTS.md` → `CONTEXT.md` → the docs your task touches.

---

## What this is

An internal HR recruitment tool ("HR Swiss Army Tools") for a manufacturing company.
HR opens **Vacancies**, collects applications in **Intake Rounds**, and reviews
**Candidates** (shortlist / flag / reject, then contact via prepared email templates).
On-prem Windows desktop deployment target — no cloud, no OAuth.

- **Backend:** ASP.NET Core monolith, Vertical Slice Architecture over Clean Architecture
  layers (`src/hr-sat.Domain` / `hr-sat.Application` / `hr-sat.Infrastructure` /
  `hr-sat.Web.Api`). EF Core + PostgreSQL.
- **Frontend:** Vue 3 SPA (`src/hr-sat.Client`) — Composition API, Nuxt UI v4 as the UI
  source of truth, Tailwind CSS v4, Geist typeface, light-only theme. Feature-first
  slices: `src/pages/<page>/` composes `src/features/<feature>/`.
- **Tests:** backend xUnit + WebApplicationFactory + Testcontainers
  (`tests/hr-sat.Tests/`); frontend Vitest + VTU + jsdom
  (`src/hr-sat.Client/src/**/*.spec.ts`). Flow-first: every test names a user story
  (`US-nn`) or a `CONTEXT.md` term, or it doesn't get written.

## Current state

- **Shipped (V1):** vacancy CRUD, intake rounds, `.eml` email import → candidates with
  PDF CV documents, review workspace (PDF-dominant), review status/notes/requirement
  reviews, email templates + prepared messages, hire outcomes, promote/bench/runaway.
- **Deferred (V3):** OCR extraction of CVs + full-text requirement matching
  (ADR-0009 — PdfPig + Sdcb.PaddleOCR; `ExtractionStatus` field already exists).
- **Designed, NOT built:** **form-intake** — Google Forms CSV as a second intake source,
  per-vacancy ordinal-keyed Form Layout, screening rules. Fully specced 2026-09-16; see
  "Open work" below. Nothing of it exists in code yet — the importer is `.eml`-only.

## Run / verify

- Solution: `hr-sat.slnx` (see `global.json` for the pinned .NET SDK).
- Server tests: `dotnet test tests/hr-sat.Tests/hr-sat.Tests.csproj`
  (needs Docker for Testcontainers PostgreSQL).
- Client (from `src/hr-sat.Client/`): `npm run dev`, `npm run test` (Vitest),
  `npm run type-check`, `npm run lint` (oxlint only — ADR-0001), `npm run build`.

## Conventions that bite

- **Vertical slices only** — schema → API → client → seam tests, per
  `docs/agents/workflow.md`. No horizontal tickets. **No TDD**: tests after
  implementation, in the same ticket.
- **Glossary is law.** Terms in `CONTEXT.md` are canonical; each has an `_Avoid_` list.
  If your task's language conflicts with the glossary, stop and resolve the term first.
- **Nuxt UI direct** — no `App*` wrappers, no resurrecting the deleted `src/shared/ui/`
  (ADR-0007). Theme only via `@theme` in `style.css` + `ui` config in `vite.config.ts`.
- **Shared code is earned** — moves to `Shared/` only at the second same-reason consumer.
- **Lifecycle discipline** (ADR-0010): closed rounds are read-only for review data;
  hire-outcome bookkeeping stays editable until vacancy close; *Settled* data is changed
  only through the lifecycle's own powers.
- **Commits:** the owner pushes; agents never commit autonomously (AGENTS.md).

## Doc map

- `CONTEXT.md` — domain glossary (single context).
- `docs/adr/` — 14 ADRs; load-bearing ones: 0002 (stack + API seam), 0003 (flow-first
  testing), 0005 (vertical-slice-dotnet conventions), 0007 (Nuxt UI), 0008 (client design
  language), 0009 (OCR matching, V3), 0010 (intake rounds), 0011 (client rules in agent
  skills), 0012 (dependency version policy).
- `docs/discovery/` — product overview, business process, **user stories** (the backlog),
  architecture, UI sketches, database design.
- `docs/agents/` — workflow, testing, dotnet, vue, domain, issue-tracker, triage-labels.
- `.scratch/<feature>/` — work items: `spec.md` + `issues/NN-<slug>.md`
  (`Status:` line per issue; `ready-for-agent` means unblocked).

## Open work: form-intake (next up)

Specced 2026-09-16 in a grill session; do not re-derive the design.

1. `.scratch/form-intake/spec.md` — the shape and non-goals.
2. `.scratch/form-intake/issues/` — 4 vertical slices: `01-csv-form-response-import` →
   `02-form-layout-config` → (`03-screening-rules` ∥ `04-review-page-form-variant`).
3. Decisions: `docs/adr/0013` (form intake + ordinal-keyed layout) and
   `docs/adr/0014` (screening = computed disposition, never deletion); ADR-0008 amendment
   (evidence-dominant review variant for form candidates).
4. New glossary terms to honor: *Intake Source, Form Response, Form Layout, Header Drift,
   Screening Rule, Screened Out, Resubmitted*.

Hard constraints: columns keyed by **ordinal**, never header text; screening never deletes
and never sets Review Status; CV is a Drive **link** opened externally (no iframe, no
fetch); no type parsing of free-text columns in V1; candidate list moves to server-side
100/page once screening lands.

## After form-intake

- `.scratch/v3/spec.md` — the deferred OCR/extraction pipeline (ADR-0009).
- `.scratch/v2/spec.md`, `.scratch/ca-migration/map.md` — older efforts; check their
  issue `Status:` lines before assuming relevance.
