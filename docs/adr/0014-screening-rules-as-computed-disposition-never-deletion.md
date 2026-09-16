# ADR-0014: Screening rules as a computed disposition, never deletion

## Status

Accepted

## Context

HR screens form responses with hard knockouts — the canonical example from the real CSV:
drop rows where the "any department" willingness column (K) is `Tidak`, where the
professional-experience column (L) is empty, or where the CV-link column (P) is empty.
The obvious implementation is to *filter at import* — never create the candidate. That
breaks three things the glossary already promises: original content is retained; Rejected
is a human Review Status, not a machine verdict; and a mistyped rule silently deletes
people with no audit trail.

## Decision

Screening is a **computed disposition, not a deletion and not a review status.**

1. **Import everything.** Every form row becomes a stored candidate with its raw Form
   Response. No row is ever dropped at import.
2. **Screened Out is computed.** A candidate failing at least one Screening Rule is tagged
   *Screened Out* — a display disposition layered on top of Review Status, which stays
   `new`. The glossary reserves *Rejected* for HR's own decision; *Screened Out* is the
   machine's.
3. **Hidden, countable, reclassifiable.** Screened-out candidates are excluded from the
   default candidate list behind a count badge and toggle ("37 screened out"), each row
   showing which rule(s) fired. There is no delete affordance. Editing the vacancy's rules
   re-evaluates live against stored raw responses, so a fixed rule re-surfaces its victims
   for free.
4. **Rules are vacancy-owned and live.** Up to 5 rules per vacancy, AND-combined, over the
   raw text of one form column each (`equals` / `not-equals` / `is-empty` / `not-empty` /
   `contains`). No type parsing — the salary column's free text ("Bacik", "5,3",
   "6000.000-7000.000/month") is evidence enough that parsing is a trap. Email-sourced
   candidates have no form columns and are never screened.
5. **Settles with the round.** Screening status is review data under ADR-0010: it freezes
   at round closure. Editing rules after closure affects only the active round and future
   imports.
6. **Live count is the safety net.** The rules editor shows a live "would screen out N of
   M" count before save, with an amber warning on a rule that matches nothing or
   everything. This is the deliberate countermeasure to the silent-disaster failure mode.

## Considered options

- **(a) Filter at import (drop rows)** — rejected: irreversible, unauditable, and breaks
  "original content is retained."
- **(b) Auto-set Review Status = rejected** — rejected: *Rejected* is defined in the
  glossary as HR's decision; a machine must not wear that word.
- **(c) Type-aware rules (number ranges, yes/no normalization)** — deferred: V1 operators
  are text-only; range filtering waits until HR actually screens by salary.

## Consequences

- `Screening Rule` and `Screened Out` enter [CONTEXT.md](../../CONTEXT.md) as first-class
  terms.
- The candidate list query gains a server-side screening disposition (needed anyway once
  the list paginates — see the form-intake spec).
- Rule evaluation must be cheap and total over a round's stored raw responses; it runs in
  the read path, not the write path.
- A future "bulk reject the screened-out" action can layer on top later without touching
  this decision.
