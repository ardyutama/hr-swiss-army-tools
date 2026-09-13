# 03: Promote leftover candidates between rounds

**What to build:** HR can **Promote** candidates from a closed round into the active
round (ADR-0010). From the active round's candidate list, a "Promote from Round N…"
action opens a multi-select picker of that closed round's **promotable** candidates:
Review Status New/Flagged/Shortlisted **and** no hire outcome. Rejected candidates and
those with a current hire outcome (hired/runaway/declined) are excluded dynamically;
clearing an outcome makes a candidate promotable again. Promoting is a **move**, not a
copy (one source email → one candidate): review status, requirement reviews, and notes
carry over; promotion only accepts candidates whose outcome is already none. The record
gains a display-derived history line: *"Promoted from Round 1 on 2026-09-10."*

**Blocked by:** V2 ticket 00-intake-rounds; outcome exclusion refines once 02 lands

**Status:** ready-for-agent

- [X] Promote endpoint: move selected promotable candidates from a closed round into the
  active round; lifecycle violations return 409 and stale/non-promotable selections return 422
- [X] Promotable = New/Flagged/Shortlisted with no hire outcome; others excluded
- [X] Review status, requirement reviews, and notes preserved; outcome remains none
- [X] History line derived from data, shown on candidate detail — no audit subsystem
- [X] Picker UI on the active round's candidate list (multi-select from a chosen closed round)
- [X] Backend and frontend tests pass (written after implementation)
