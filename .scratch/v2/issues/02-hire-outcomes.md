# 02: Hire outcomes (hired / runaway / declined)

**What to build:** Candidates gain a **Hire Outcome** field: `none / hired / runaway /
declined`, separate from Review Status. In the review workspace, shortlisted candidates get
outcome actions: Mark Hired, Mark Runaway (only when hired), Mark Declined, and Clear
Outcome (revert to none). Marking a hired candidate Runaway reopens one slot — the vacancy
list shortage increases by one. All outcome changes are rejected when the vacancy is
closed. Glossary: Hired Candidate, Runaway, Declined, Shortage (CONTEXT.md).

**Blocked by:** V2 ticket 01-needed-hires

**Status:** ready-for-agent

- [ ] Hire Outcome persisted per candidate; defaults to none
- [ ] Outcome actions on shortlisted candidates in the review workspace; review status untouched
- [ ] Transitions enforced: hired only from shortlisted; runaway only from hired; revert to none allowed
- [ ] Runaway re-increments the shortage; Shortage/hired counts on the vacancy list update
- [ ] Outcome changes rejected (409) when the vacancy is closed
- [ ] Backend and frontend tests pass (written after implementation)
