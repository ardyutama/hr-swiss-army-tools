# ADR-0017: ScreeningContext as the screening-evaluation seam

## Status

Accepted (2026-09-19; implements the ScreeningContext slice from the same-day grill session)

## Context

Screening evaluation was assembled ad hoc at every read site. Each consumer passed the
same triple — `roundClosed`, the `ScreeningRuleSet`, and the `FormLayout` — into
`Candidate.EvaluateScreening`, and each re-derived the same decision: whether to evaluate
live against the vacancy's rules, read the frozen Screening Verdict, or screen nobody.
That decision is one policy, but it lived in five places:

- the candidate-details reader fetched the round's closed-ness, the rule set, the layout,
  and the candidate's form responses in **separate queries**, then evaluated;
- the review queue and the promote summary each loaded the rule set and layout and
  re-derived closed-ness;
- Vacancy Progress evaluated **twice** per candidate (once for the progress filter, once
  for the review-counts filter);
- the rules preview bypassed the shared shape entirely.

The triple is a data clump: three values that travel together and are only ever
interpreted one way. Assembling the evaluation inputs ad hoc meant every new consumer had
to re-learn the dispatch rule (live vs frozen vs none) and re-issue the same loads.

## Decision

Introduce **`ScreeningContext`** (in `Domain/Vacancies`, beside `ScreeningRuleSet`): a
sealed value that names how one candidate's screening is evaluated, constructed at the
point that already knows the vacancy's rules and the round's lifecycle state.

- **Three cases, one type.** `Kind` is `Live | Frozen | None`. `Live(ruleSet, layout)`
  evaluates the rules against the current form response; `Frozen` reads the stored
  Screening Verdict written at round closure; `None` screens nobody (a vacancy with no
  rules or no layout).
- **The construction sites own the policy.** The live/frozen decision is made where the
  facts already live: `ScreeningContextLoader` short-circuits a closed round to `Frozen`
  with **zero queries**; the promote summary builds `Frozen` directly because its
  source-round guard already proves closure; the rules preview builds `Live` directly on
  a *prospective* rule set that comes from the query, never the store. Dispatch over
  `Kind` stays on `Candidate` — it owns the Intake Source, the stored verdict, and the
  form responses.
- **Null tolerance has exactly one door.** `Of(ruleSet?, layout?)` normalizes a missing
  rule set or layout to `None`; `Live` rejects nulls, and `FreezeScreening` throws on a
  `Frozen` input — both are programmer errors surfaced at the boundary, not silent
  no-ops.
- **The verdict is read once, at birth.** Vacancy Progress's `CandidateRound` now
  evaluates the context once at construction and both filters read the stored result,
  halving the evaluations on that path.

`ScreeningContextLoader` (in `Application/Features/Shared`) is the batch/live loader:
`LoadAsync` for the single-vacancy case, `LoadLiveAsync` for Vacancy Progress's
per-vacancy batch. It is earned shared code (second same-reason consumer), placed by the
`VacancyWrite` precedent. The ADR-0016 candidate-list seam is unchanged: the test-side EF
adapter consumes screening through the same `Of`/`Frozen` door, so both adapters still
satisfy the shared characterization suite.

## Consequences

- One named seam owns how screening is evaluated; the five call sites no longer
  re-derive the live/frozen/none decision or re-issue its loads.
- The candidate-details reader drops a redundant candidate query (the double fetch);
  the promote summary drops two dead loads; Vacancy Progress drops the second
  evaluation per candidate.
- `EvaluateScreening` and `FreezeScreening` take a single `ScreeningContext` — a
  big-bang signature change, no parallel overload — and `CandidateSummaryMapper.Map`
  follows.
- No wire/DTO changes, no SQL evaluation of screening beyond the ADR-0016 paged list,
  and no CONTEXT.md term: "evaluated live" and the frozen Screening Verdict are already
  glossary language; `ScreeningContext` is a module name.
- **Scope boundary (reference, do not re-argue):** ADR-0016 owns the
  no-SQL-push-for-screening boundary and its amendment records card 4's closure. Review
  cards 5 (progress hydration) and 6 (jsonb/CASE/batching) stay deferred.
