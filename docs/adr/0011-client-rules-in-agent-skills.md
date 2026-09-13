# ADR 0011: Client rules live in agent skills, ADRs record decisions

## Status

Accepted (2026-09-09).

## Context

The client's living rules were recorded in three places that disagreed with each other:
`docs/agents/vue.md` carried full rule bodies, the ADRs (0001, 0007) carried the same
rules as decision bullets, and `.agents/skills/` held the agent-facing guidance. The three
drifted: ADR-0007 still said feature `validation.ts` modules were "retired" when they had
in fact become live Zod schemas feeding `<UForm :schema>`, and `vue.md` duplicated the
skill rules it claimed to supplement.

A future architecture review, seeing ADRs as authoritative, could "correct" the pointers
back to the ADRs and reintroduce the duplication — unless the inversion is itself recorded
as a decision.

## Decision

For the **Vue client**, the single source of truth for *living* rules is
`.agents/skills/vue-feature-slices/SKILL.md`. `docs/agents/vue.md` becomes a thin pointer:
it keeps only the workflow (Start Here), the completion check, and the repo-specific deltas
the skill does not carry. ADRs record *that* a decision was made, *why*, and *when* — they
stop carrying the living rule text and instead point at the skill.

This inverts the usual doc hierarchy for the client only. Backend rules are unchanged:
`vertical-slice-dotnet` remains the backend slice source of truth per ADR-0005.

## Consequences

- A rule change for the client edits one file: the skill. The ADR is not amended for rule
  refinements, only for reversals.
- ADR-0007's stale "validation.ts modules are retired" line is corrected as part of this
  change (they became Zod schemas; they were not deleted).
- ADR-0002's inline client-shape description is replaced by a pointer to the skill.
- `AGENTS.md` and `docs/agents/workflow.md` point at the skill, not at restated rule bodies.
- A future review should not re-suggest "make the ADR authoritative for client rules"
  without retiring this ADR first.
