# ADR-0016: Candidate list read seam with contract-tested dual adapters

## Status

Accepted (2026-09-19, grill-with-docs session; design only, implementation follows)

## Context

The candidate list is the app's central screen (US-14, sketch S3). Its read path grew an
optional seam: `ListCandidatesQueryHandler` took `ICandidateListReader? = null`, and when
null (every handler unit test, via the SQLite `TestDbContext`) a ~60-line LINQ fallback
re-implemented filtering, counts, paging, and Screened-Out evaluation. Production
(Testcontainers-backed) used `PostgresCandidateListReader`, whose raw SQL re-implements
the same Screening Rule semantics. The spec therefore existed twice — in SQL and in C# —
and the two paths could silently diverge on trim/case/empty semantics: SQL decided page
membership and counts while C# decided the per-row badge, so a divergence could show a
Screened Out badge on a row sitting in the unscreened page. Separately, the reader
returned only page IDs + counts, forcing the handler to re-fetch full candidate graphs
and re-evaluate screening in C#.

Deleting the SQL reader (one spec, LINQ-only) was considered and rejected: the ADR-0014
amendment explicitly sanctions the jsonb translation for the paged list in
Infrastructure, and reversing that is a bigger decision than this seam-hardening.

## Decision

The seam is **mandatory, row-shaped, and policed by a shared characterization suite**:

1. **Mandatory seam, dual adapters.** `ICandidateListReader` is a required dependency of
   `ListCandidatesQueryHandler`; the LINQ fallback is deleted. Two adapters implement it:
   `PostgresCandidateListReader` (production, raw SQL over the jsonb rule set) and an
   EF/LINQ adapter **in the test project only** — the C# spec's legitimate home is the
   suite that guards the SQL one; Infrastructure keeps a single production adapter until
   a second production consumer earns it (shared code is earned, `AGENTS.md`).
2. **Full page rows, not IDs.** The reader returns page rows carrying every
   `CandidateSummaryResponse` scalar, the CV count, `ScreenedOut`, and the fired rules —
   plus counts — in one read. The handler keeps only vacancy/round guards, parameter
   parsing, and DTO assembly; it never fetches the rule set, layout, or candidate graphs.
3. **Domain authors all display text.** SQL never formats. Active rounds: SQL returns
   fired-rule **indexes** (via `WITH ORDINALITY` over the rule array); Infrastructure
   maps index → display through a new domain API on `ScreeningRuleSet` (alongside
   `EvaluateWithDisplay`). Closed rounds: the SQL returns the stored, frozen
   `screening_verdict` jsonb **verbatim** — because Screening Rules are editable while a
   vacancy is open, a frozen index re-resolved against the *current* rule set could point
   at a changed rule; the stored verdict is the only safe read. Both cases materialize as
   one homogeneous row shape: `ScreenedOut` + `IReadOnlyList<ScreeningRuleMatch>`.
4. **One characterization suite, two runs by construction.** An abstract test class
   (`tests/hr-sat.Tests/Candidates/`, e.g. `CandidateListReadContractTests`) carries
   identical scenarios — all five operators with trim/case/empty edges, scope toggle,
   status/outcome/query filters, both sort directions, paging, counts, closed-round
   frozen verdict, and email-sourced candidates never screened. Two concrete subclasses
   run it: the EF/LINQ adapter on the SQLite `TestDbContext`, and
   `PostgresCandidateListReader` on Testcontainers PostgreSQL. Both seed through EF
   (`TestDbContext` / migrated `AppDbContext`) so fixtures are identical by construction;
   there is no EF-adapter-on-Postgres run, because nothing deploys that configuration.

`PageSize` (100) moves onto `CandidateListReadRequest` as the single seam-level owner;
the new `CandidateListReadRow` record lives in `Abstractions/Data` beside the existing
request/result/counts records. The change is internal-only: no wire/DTO changes, and
`Program.cs` registration is untouched (the seam goes from optional to mandatory with no
DI churn).

## Considered options

- **(a) Delete the SQL reader; one LINQ spec, hardened projection** — rejected: re-litigates
  the ADR-0014 amendment's sanction of jsonb translation in Infrastructure.
- **(b) Mandatory seam but keep IDs + counts result** — rejected: keeps the `Contains(id)`
  re-query and the handler-side re-evaluation the redesign exists to remove.
- **(c) SQL returns fired-rule display strings** — rejected: display text would have two
  authors (SQL + domain `ScreeningRuleSet.FormatDisplay`), re-introducing a
  format-divergence bug class.
- **(d) EF/LINQ adapter in Infrastructure** — rejected: production has one consumer;
  shipping a second adapter there is dead code under the shared-earned rule.

## Consequences

- The Screening Rule spec still exists in two languages (SQL + C#), now **deliberately**,
  with the contract suite as the loud-failure tripwire. Future reviewers should not
  re-flag the duplication without also proposing to retire the suite.
- `Screening Verdict` enters [CONTEXT.md](../../CONTEXT.md) as a first-class term.
- The handler shrinks to guards + assembly; `GetScreening`/`ToRow`/`Matches*` helpers and
  the `CandidateListRow` type die with the fallback.
- Follow-up (deferred, separate slice): the reader's three commands (counts, totals, page)
  can batch into one round trip — see the 2026-09-19 architecture review, card 6.
- 2026-09-19 follow-up (grill session, candidates 3+4): the review's card 4 —
  "`CandidateSummaryMapper` has an unshared twin" — is **closed as dissolved**. The twin was
  two entity→response field copies; this ADR's implementation deleted the handler's copy.
  What remains (`CandidateSummaryMapper` over the entity, `ToResponse` over
  `CandidateListReadRow`) maps two different source shapes with screening pre-computed
  behind the seam — per-adapter mapping costs the characterization suite already polices,
  not an earnable shared module.
