# ADR 0003: Flow-first testing focus

## Status

Accepted (2026-08-30). Amended by ADR-0005 (2026-08-31): backend slices also carry
handler unit tests and validator tests (mechanics from the `vertical-slice-dotnet`
pack); flow-first scope and traceability still decide what gets tested.

## Context

`docs/agents/workflow.md`, `.scratch/mvp/spec.md`, and `docs/agents/vue.md` previously
directed tests to the declared seams (HTTP API; feature component) and mandated coverage of
loading, error, empty, ready, disabled, and submission states. In practice this produced
component-mechanics tests (badge text, hidden buttons, empty states) whose maintenance cost
tracks the UI, not the business. The maintainer wants tests that prove the business process
and user flows behave correctly — and only those.

## Decision

Testing is **flow-first**. Every test must trace to a user story (`US-n`) or a `CONTEXT.md`
glossary term, named in the test itself. Stories on the critical spine (US-9, US-12/13,
US-17/18, US-19) must land their Flow Tests in the same ticket. State Tests survive only
when the state is itself a business rule. Pure-function unit tests survive only for
traceable business rules. Seams do not move: HTTP + Testcontainers on the backend,
Vitest + VTU with only `fetch` mocked on the frontend; no E2E tooling without a separate
ADR. Full rule: `docs/agents/testing.md`.

Superseded: the "Testing stance" section of `docs/agents/workflow.md`, the "Testing
Decisions" section of `.scratch/mvp/spec.md`, and the state-coverage mandate in
`docs/agents/vue.md` (their seam and work-order points are kept and now live in
`docs/agents/testing.md`).

## Consequences

- Component-mechanics coverage (empty states, badges, render checks) is deliberately given
  up for features where it isn't a business rule; regressions there are caught by Flow
  Tests or not at all.
- Existing pre-rule suites are frozen, not purged or retrofitted; the mix of old-style and
  new-style tests in the tree is expected and not a violation.
- "Critical functionality" is defined objectively by the spine list, so review can check
  coverage mechanically.
- Future reviews should not re-suggest broad state-coverage mandates without a concrete
  defect class that Flow Tests demonstrably miss.
