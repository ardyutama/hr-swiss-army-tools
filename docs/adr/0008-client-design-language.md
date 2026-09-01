# ADR-0008: Client design language — warm-utilitarian, light-only, sans display

## Status

Accepted

## Context

The client design was reviewed in a grill-with-docs session (2026-09-01). The settled
direction: preserve and evolve the existing theme rather than overhaul; the app is a
dense internal productivity tool, not a marketing surface, so landing-page design rules
do not apply. Inter was loaded as the default sans but was consciously retired: it reads
as the generic AI/SaaS default, and the product is allowed one deliberate typographic
voice. Dark mode was deferred (internal office tool on shared Windows PCs; dual-mode
doubles every color decision and test surface). The design-taste frontend skill applies
selectively: anti-slop discipline, typography, color/shape consistency locks, and layout
discipline only — not hero/bento/marquee rules.

## Decision

1. **Typeface**: drop Inter; use **Geist** (via `@fontsource/geist`, weights 400–700)
   as the single UI typeface. Geist is neutral enough for non-technical users but not
   the LLM-default voice. No serif anywhere. No mono except tabular numbers where data
   density demands it (progress counters, dates).
2. **Theme lock (light-only)**: keep the existing warm-neutral tokens —
   background `#f7f6f3`, ink `#1e2430`, sidebar `#202634`/`#c2c9d6`, primary `#4361a8`,
   success `#1c7c43`, error `#c94f4f`, radius 12px — as the locked palette. One accent
   (primary blue) per screen; status colors reserved for semantic state only.
3. **Density**: working-dense (5–6/10): compact table rows, ≥40px hit targets, generous
   whitespace *between* sections, not inside rows.
4. **Shape lock**: one radius scale — 12px on cards/dialogs/inputs, full-pill on badges
   and small action buttons. No mixed systems.
5. **Motion**: restrained (3–4/10): CSS transitions on transform/opacity only; honor
   `prefers-reduced-motion`; no scroll-driven animation, no marquees, no perpetual loops.
6. **Design source of truth**: Nuxt UI v4 remains the component layer per ADR-0007; all
   theming flows through `@theme` in `style.css` and the `ui` config in `vite.config.ts`.
   No `App*` wrappers, no per-component color improvisation.
7. **Review workspace (S4) layout**: PDF-dominant. The CV viewer takes ~55–60% of the
   content width because source CVs are design-heavy and extraction is imperfect; the
   data column (requirements, candidate details, email, notes) is the supporting
   checklist at ~40–45%.
8. **Review action bar**: mouse-first buttons with visible keyboard hints (`kbd` chips:
   `←`/`→` navigate, `A`/`F`/`R` decide). Hit targets ≥40px on this bar. Power users
   grow into shortcuts; nobody needs a tutorial.
9. **Notes commit contract**: a review decision (Shortlist/Flag/Reject) silently saves
   pending notes; Prev/Next navigation auto-saves too. No explicit Save dependency, no
   unsaved-changes warnings. The backend slice must honor decision-as-commit.
10. **Tables are semantic**: both VacancyTable and CandidateList use semantic `<table>`
    markup. VacancyTable's custom grid is refactored away — density 5–6 does not need
    layout tricks, and keyboard/screen-reader support comes free.
11. **Decision verbs, status adjectives**: action-bar buttons use glossary verbs
    (**Shortlist / Flag / Reject**); status badges use glossary adjectives
    (Shortlisted / Flagged / Rejected). The sketch's "Accept" is retired — it conflicts
    with the CONTEXT.md "_Avoid_: Accepted candidate" rule.
12. **Review workspace is a route**: `/vacancies/:id/review/:candidateId` — deep-linkable,
    browser back returns to the candidate list, Prev/Next and keyboard shortcuts map to
    router navigation.
13. **Review left panel**: Requirements and Candidate Details pinned and always visible;
    Source Email (subject/body) collapsed by default with an explicit "Open" affordance;
    Notes always visible at the bottom, adjacent to the action bar. No accordions —
    nothing decision-relevant hides during triage.
14. **State language**: shape-matched skeletons for tables and review panes; a plain
    centered loading label for fast operations; errors render inline in the region that
    failed, never toast-only; empty states are one sentence plus one action button.
15. **No dead UI**: the sidebar's "Templates (soon)" and S1's reserved "No Idea" slots
    are removed from rendered UI and tracked as tickets instead. Placeholders teach
    users to ignore the interface. The dark sidebar itself stays — it is the strongest
    brand element.

## Consequences

- `package.json` swaps `@fontsource/inter` for `@fontsource/geist`; `style.css` imports
  change. Future dark mode remains possible because all colors are CSS variables, but
  is not built in V1.
- Every future screen (S4 review workspace, S5 bulk email) inherits this grammar; S4's
  pane proportions and action bar set the density precedent.
- A future reader wondering "why Geist, why no dark mode, why this blue" finds the
  answer here instead of in scattered component files.
