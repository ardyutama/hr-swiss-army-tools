# ADR 0015: PostgreSQL practices live in the postgresql-best-practices skill

## Status

Accepted (2026-09-19).

## Context

PostgreSQL guidance had no authoritative home. `docs/agents/dotnet.md` covers EF Core
mechanics but not PostgreSQL-side design (indexing, full-text search, JSONB layout,
partitioning, pooling), and implementing ADR-0009 (full-text requirement matching) and
ADR-0013 (ordinal-keyed form layout) needs exactly those patterns. The
`microsoft/postgres-skills` pack now provides them at
`.agents/skills/postgresql-best-practices/` — local-only agent tooling like the other
packs (`.agents/` is gitignored).

## Decision

`.agents/skills/postgresql-best-practices/` is the **source of truth for PostgreSQL best
practices and implementation patterns**, the same arrangement ADR-0005 and ADR-0011 made
for the backend and client packs. Its `SKILL.md` routes to per-topic references; repo
docs point at it and stop restating practice.

Boundaries:

- `docs/agents/dotnet.md` keeps EF Core mechanics (Npgsql mapping, the `dotnet ef`
  migration workflow, read-path query shape). The skill owns PostgreSQL-side design and
  operations.
- The project runs self-hosted PostgreSQL 18 (Compose dev, Testcontainers tests, on-prem
  deployment). The skill's `postgresql-*` references apply; its Azure guardrails and
  `azure-postgresql-*` references stay dormant unless a managed Azure deployment is
  adopted.

## Consequences

- PostgreSQL practice questions are answered from the skill; repo docs keep only deltas
  the skill cannot know (deployment target, version pin, doc boundaries).
- Schema and migration work consults the skill's indexing, FTS, and JSONB references
  before introducing new database patterns.
- Adopting a managed Azure PostgreSQL deployment later activates the skill's Azure
  guardrails with no doc change.
