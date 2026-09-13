# V2 Spec — Hire Tracking and Intake Rounds

Extends the V1 MVP. Requires V1 complete (or at least through candidate review).

## Problem

Recruitment is a batch process: one vacancy needs several hires, filled in **waves** —
collect a wave of applications, review, hire, and if slots remain, open another wave.
After shortlisting and contacting, some candidates get hired — and some hires no-show,
quit, or disappear while hiring is still ongoing ("runaway"). HR needs to know how many
slots of the batch are still open at any moment, and which wave produced which hires.

## Scope (revised 2026-09-06 — supersedes 2026-09-03)

- **Intake Rounds**: every vacancy is structured as rounds (ADR-0010). A vacancy created
  without waves lives its life in its one default round. One active round per vacancy;
  none between waves is legal (imports rejected). Rounds are created and closed manually,
  never auto-spawned or auto-closed; closed rounds are read-only forever (review data;
  hire outcomes on their candidates stay editable while the vacancy is open). Round identity:
  per-vacancy `RoundNumber` (1..n, never reused) plus optional name.
- **Candidate identity**: one person + one round + one source email = one candidate.
  Existing vacancies migrate into a default Round 1 with all their candidates.
- **Promote**: explicit move of New/Flagged/Shortlisted-no-outcome candidates from a
  closed round into the active round; review status, requirement reviews, and notes
  carry over; outcome resets to none. Rejected and terminal-outcome candidates are
  locked to their round.
- **Prior Application Notice**: display-only "applied in Round N — status" on candidate
  detail/review screens, matched by normalized source sender email across the vacancy's
  other rounds; never blocks, never affects status.
- **Needed Hires**: `int?` on the vacancy (1–9999), settable at create and editable while
  open. Null = review-only vacancy (exact V1 behavior, no shortage shown). Rounds carry
  no quota.
- **Hire Outcome**: new candidate field `none / hired / runaway / declined`, separate from
  Review Status. Transitions: shortlisted → hired; hired ⇄ runaway; shortlisted → declined;
  outcome can revert to none (mistake correction). Outcomes freeze when the vacancy closes.
- **Shortage** = Needed Hires − active hires; shown on the vacancy list when Needed Hires
  is set (e.g. "8/10 hired · 2 to go"). Vacancy progress counts unchanged. A runaway's
  reopened slot belongs to the vacancy shortage — the active round's implicit goal.
- "All slots filled" shows as a badge; vacancy never auto-closes (runaways reopen slots).
  Hiring past Needed Hires is allowed — it is a planning target, not a gate; the badge caps
  at Filled and counts show n/m.
- Vacancy-detail becomes a rollup: header + round list + the active round's candidates.
  Review workspace re-homes to round scope.

## Out of Scope

- Per-round hire quotas; per-intake dates beyond ordering.
- Explicit "candidate R replaced runaway X" links.
- Runaway history / audit log (promote leaves a display-derived history line only).
- Cross-vacancy duplicate detection (prior-application notice is per-vacancy only).
- Candidate-list filter by outcome (add later only if needed).

## Tickets

- [00-intake-rounds.md](issues/00-intake-rounds.md)
- [01-needed-hires.md](issues/01-needed-hires.md)
- [02-hire-outcomes.md](issues/02-hire-outcomes.md)
