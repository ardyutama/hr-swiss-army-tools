# Backend CA migration — wayfinder map

Effort slug: `ca-migration`. Tracker: local markdown per `docs/agents/issue-tracker.md`.

## Destination

A complete, sequenced migration plan for moving the hr-sat backend from ADR-0004
conventions (static `HandleAsync`, `AddValidation()`, `DomainValidationException`,
`EnsureCreated()`, integration-only tests) to the ADR-0005 Clean Architecture pack —
every route decision resolved and handed off as execution tickets. The map itself
changes no production code: planning only (settled in charting, Q1a).

## Notes

- Domain: .NET backend convention migration. The domain glossary in `CONTEXT.md` is
  untouched by this effort; no new domain terms are expected. Architecture decisions
  that prove hard to reverse may earn ADRs as tickets resolve them.
- Skills every session consults: `grilling` + `domain-modeling` for decision tickets;
  the `.agents/skills/vertical-slice-dotnet/` pack is the target being planned toward
  (source of truth per ADR-0005); `docs/agents/dotnet.md` authority order applies.
- Current-state facts are in repo memory: `backend-vsa.md` (slice facts),
  `ef-migration-state.md` (deferrable-constraint migration notes).
- Settled in charting (2026-08-31, user confirmed all recommendations):
  1. Destination is the plan only; execution hands off to workflow tickets.
  2. Foundation-first sequencing: mechanical project split → kernel/abstractions →
     EF Initial migration, then per-slice conversion.
  3. Hand-rolled minimal SharedKernel; no port of Milan's template repo (drags
     users/auth/caching baggage).
  4. Four projects under `src/`: `hr-sat.Domain`, `hr-sat.Application`,
     `hr-sat.Infrastructure`, `hr-sat.Web.Api` (retires `hr-sat.Server`); one test
     project, renamed `hr-sat.Tests`, holding `TestDbContext` and base classes;
     NetArchTest suite extended across projects.
  5. Hybrid unit-test data strategy: in-memory `TestDbContext` for pure slices;
     lock/SQL-heavy slices (UpdateVacancy, PurgeVacancy, DeleteCandidate,
     ImportCandidates) keep lock/SQL behavior at the Testcontainers seam only.
  6. Existing flat integration tests port per slice into pack shape, then retire —
     ADR-0003's grandfathering protected them from folder moves, not from the
     convention migration.
  7. Byte-stable HTTP seam: no status code, response contract, ProblemDetails shape,
     or validation-key changes; existing integration tests are the migration guard
     and must pass unmodified throughout. (Message *text* is free; keys are not —
     the client lowercases them.)
  8. Domain events: machinery only (`Entity` base, `IDomainEvent`, dispatch on
     save); slices raise no events until a real subscriber exists. No speculative
     event catalog.
  9. All 11 use cases migrate within this effort: vacancy spine first (Create →
     Get → List → Update → Close/Reopen → Purge), then candidates (List →
     GetCvDocument → Delete → Import last).

## Decisions so far

<!-- the index: one line per closed ticket, enough to judge relevance, then zoom the link -->

- [Inventory the kernel types the pack assumes](issues/01-kernel-inventory.md): ~25
  assumed types across 4 layers; `ValidationDecorator` is the only decorator; 5 types
  referenced-but-never-defined (their shapes are ours to design); pack assumes `Guid`
  Ids + an auth stack we lack. Findings: branch `research/kernel-inventory`,
  `docs/research/2026-08-31-ca-pack-kernel-inventory.md`.

## Not yet specified

- Entity-base adoption side effects: `Id`-based equality semantics vs the current
  sealed-POCO entities and their shadow-FK EF configuration. Graduates only if the
  kernel-design ticket reveals friction.
- Candidate read-side slice deviations (GetCvDocument file streaming, ListCandidates
  match computation): expected to follow the playbook mechanically; graduates if the
  playbook ticket can't cover them cleanly.
- Whether folding `VacancyWrite`/`FindVacancyForUpdateAsync` surfaces a cross-slice
  data-access pattern the sharing taxonomy doesn't place cleanly.

## Out of scope

- Executing the migration. The destination is the decided plan; execution is handed
  off as ordinary tickets under `docs/agents/workflow.md`.
- Frontend changes. The HTTP seam is byte-stable by constraint; the client never
  sees the migration.
- Domain events beyond the machinery, the CV-extraction pipeline, and any new
  feature work.
- MediatR, or re-litigating ADR-0005's target conventions (settled).
