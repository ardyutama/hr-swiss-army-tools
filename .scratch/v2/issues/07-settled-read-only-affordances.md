# 07: Read-only affordances on settled data

**What to build:** Hardening pass after 06. **Settled** data never offers edit affordances
in the first place: a closed round hides or disables candidate import, review-status
decisions, notes editing, and requirement reviews; a closed vacancy hides hire-outcome
editing (reopening stays a vacancy power). The friendly 409 from 06 remains as the
race-condition fallback for "the world changed under you" — another HR closing the round
mid-edit — and the mid-edit race still refreshes the view so the now-settled state is
visible immediately.

**Blocked by:** 06-friendly-settled-errors

**Status:** ready-for-agent

- [ ] Closed round: candidate list and review workspace show no edit affordances (import,
  review status, notes, requirement reviews)
- [ ] Closed vacancy: hire-outcome controls hidden or disabled
- [ ] Closed-round candidate data renders read-only with a visible "closed round"
  indicator
- [ ] A 409 from a mid-edit race still surfaces via the 06 mechanism and refreshes the
  view
- [ ] Seam tests cover both the hidden affordances and the race-condition fallback
