# 04: Prior application notice

**What to build:** A display-only **Prior Application Notice** on the review workspace:
when the candidate's normalized source sender email matches a candidate in another round
of the same vacancy, show e.g. *"This sender also applied in Round 1 — rejected."*
Matching is by normalized sender email only, same-vacancy only, and includes rejected
candidates (a repeat rejected applicant is exactly what HR wants to see). The notice never
blocks import, never auto-sets status, and is not a link to the other candidate — pure
lookup, display-only.

**Status:** ready-for-agent

- [x] Query: normalized source sender email match across the vacancy's other rounds
- [x] Notice on review workspace: "applied in Round N — status"
- [x] Includes rejected/flagged/shortlisted prior applicants; same-vacancy only
- [x] Display-only: no blocking, no status change, no cross-candidate link
- [x] Backend and frontend tests pass (written after implementation)
