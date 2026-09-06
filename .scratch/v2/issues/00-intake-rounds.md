# 00: Intake rounds — candidate-owning restructure

**What to build:** Restructure candidates from vacancy-owned to round-owned (ADR-0010).
A vacancy gains an ordered list of **Intake Rounds** (`RoundNumber` per vacancy, 1..n,
never reused; optional `Name`; open/closed; `ClosedAt`). Every vacancy always has rounds:
existing vacancies migrate into a default Round 1 holding all their current candidates.
Exactly one round can be active (open) per vacancy; **none active** is a legal state
(imports rejected until a round is opened). Rounds are created and closed manually —
never auto-spawned, never auto-closed. Closing with no successor is allowed; closing is
irreversible. This ticket is a restructure: no new user-facing feature beyond the round
chrome, and V1 flows must behave identically inside the default round.

**Blocked by:** V1 complete (candidate review through workspace)

**Status:** ready-for-agent

- [ ] Vacancy owns rounds; candidate FK moves from vacancy to round; EF migration moves
  existing candidates into a default Round 1 per vacancy
- [ ] Create round (only when none active), close round (manual, irreversible, confirm
  when vacancy still has shortage per ticket 01 — plain confirm for now), closed rounds
  read-only (409 on writes)
- [ ] Candidate import targets the active round; rejected (409) when no round is active
- [ ] Review workspace re-homes to `/vacancies/:id/rounds/:roundId/review/:candidateId`;
  candidate list, import dialog, filters, and composables key off round
- [ ] Vacancy-detail = header + round list (row: "Round N — name", open/closed, candidate
  count) + active round's candidate list; closed rounds reachable read-only
- [ ] Quiet UI: single open round shows no round chrome — V1 screens look untouched
- [ ] Backend and frontend tests pass (written after implementation)

**Note:** Needed Hires / shortage do not exist yet (ticket 01) — the close confirmation
is a plain "close this round?" until then.
