# 02: Hire outcomes (hired / runaway / declined)

**What to build:** Candidates gain a **Hire Outcome** field: `none / hired / runaway /
declined`, separate from Review Status. In the review workspace (round-scoped since
ticket 00), shortlisted candidates get outcome actions: Mark Hired, Mark Runaway (only
when hired), Mark Declined, and Clear Outcome (revert to none). Marking a hired candidate
Runaway reopens one slot — the vacancy-level shortage increases by one; the marker stays
on the candidate record in their origin round as historical truth, and the reopened slot
belongs to the active round's implicit goal. All outcome changes are rejected when the
vacancy is closed — a closed origin round does **not** block them; rounds freeze review
data, not outcome bookkeeping (CONTEXT.md, Round Closure). Glossary: Hired Candidate,
Runaway, Declined, Shortage (CONTEXT.md).

**Blocked by:** V2 ticket 01-needed-hires

**Status:** ready-for-agent

- [ ] Hire Outcome persisted per candidate; defaults to none
- [ ] Outcome actions on shortlisted candidates in the review workspace; review status untouched
- [ ] Transitions enforced: hired only from shortlisted; runaway only from hired; revert to none allowed
- [ ] Runaway re-increments the vacancy shortage; Shortage/hired counts on the vacancy list update
- [ ] Outcome changes rejected (409) only when the vacancy is closed; a closed origin round
  allows them — the closed round's read-only candidate view still exposes the outcome actions
  while the vacancy is open
- [ ] Backend and frontend tests pass (written after implementation)
