# 04: Prior application notice

**What to build:** A display-only **Prior Application Notice** on candidate detail and
review screens: when the candidate's normalized source sender email matches a candidate
in another round of the same vacancy, show e.g. *"This person applied in Round 1 —
rejected."* Matching is by normalized sender email only, same-vacancy only, and includes
rejected candidates (a repeat rejected applicant is exactly what HR wants to see). The
notice never blocks import, never auto-sets status, and is not a link to the other
candidate — pure lookup, display-only.

**Blocked by:** V2 ticket 00-intake-rounds

**Status:** ready-for-agent

- [ ] Query: normalized source sender email match across the vacancy's other rounds
- [ ] Notice on candidate detail and review workspace: "applied in Round N — status"
- [ ] Includes rejected/flagged/shortlisted prior applicants; same-vacancy only
- [ ] Display-only: no blocking, no status change, no cross-candidate link
- [ ] Backend and frontend tests pass (written after implementation)
