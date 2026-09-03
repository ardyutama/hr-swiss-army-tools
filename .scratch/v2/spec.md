# V2 Spec — Hire Tracking

Extends the V1 MVP. Requires V1 complete (or at least through candidate review).

## Problem

Recruitment is a batch process: one vacancy needs several hires. After shortlisting and
contacting, some candidates get hired — and some hires no-show, quit, or disappear while
hiring is still ongoing ("runaway"). HR needs to know how many slots of the batch are still
open at any moment.

## Scope (settled 2026-09-03)

- **Needed Hires**: `int?` on the vacancy (1–9999), settable at create and editable while
  open. Null = review-only vacancy (exact V1 behavior, no shortage shown).
- **Hire Outcome**: new candidate field `none / hired / runaway / declined`, separate from
  Review Status. Transitions: shortlisted → hired; hired ⇄ runaway; shortlisted → declined;
  outcome can revert to none (mistake correction). Outcomes freeze when the vacancy closes.
- **Shortage** = Needed Hires − active hires; shown on the vacancy list when Needed Hires
  is set (e.g. "8/10 hired · 2 to go"). Vacancy progress counts unchanged.
- "All slots filled" shows as a badge; vacancy never auto-closes (runaways reopen slots).
- Replacement sources: promote from the same vacancy's leftover pool or import fresh
  `.eml`s — both already work; no special tooling.

## Out of Scope

- Named batch entities, per-intake dates.
- Explicit "candidate R replaced runaway X" links.
- Runaway history / audit log.
- Candidate-list filter by outcome (add later only if needed).

## Tickets

- [01-needed-hires.md](issues/01-needed-hires.md)
- [02-hire-outcomes.md](issues/02-hire-outcomes.md)
