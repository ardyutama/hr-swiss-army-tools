# ADR 0005: Clean Architecture template conventions for the backend

## Status

Accepted (2026-08-31). Supersedes ADR-0004; amends ADR-0002 (database lifecycle) and
ADR-0003 (backend test mechanics).

## Context

The skill pack in `.agents/skills/vertical-slice-dotnet/` (Milan Jovanović's Clean
Architecture template) provides a complete, executable convention set for backend slices:
scaffolding skills (`add-feature`, `add-entity`, `add-tests`) and a review skill
(`ca-review`). ADR-0004 chose direct static handlers and framework input validation as a
deliberate minimal step, and itself sanctioned owned `ICommand`/`IQuery` handler
interfaces with decorators as the fallback shape if cross-cutting concerns outgrew
framework hooks. The maintainer has decided to adopt the pack wholesale as the backend
standard.

## Decision

The pack is the **source of truth for backend slice conventions**. Wherever repo
documents conflict, the pack wins:

- **Layers.** Multi-project layout: `src/Domain` (entities, `{Entity}Errors` catalogs,
  domain events; references `SharedKernel` only), `src/Application` (one folder per use
  case under `{Feature}/{UseCase}/`), `src/Infrastructure` (`AppDbContext`, EF
  configuration, migrations), `src/Web.Api` (composition root and endpoints mirrored per
  use case under `Endpoints/{Feature}/{UseCase}.cs`).
- **Dispatch.** Owned `ICommand`/`IQuery` abstractions with
  `ICommandHandler<>`/`IQueryHandler<>` implementations, registered by Scrutor assembly
  scanning with decorators — never MediatR (commercially licensed).
- **Validation.** A FluentValidation `{Command}Validator` per command, run by the
  `ValidationDecorator` before the handler. `AddValidation()` + DataAnnotations presence
  rules retire with ADR-0004.
- **Errors.** `Result`/`Result<T>` with static `{Entity}Errors` factories
  (`"{Feature}.{Reason}"` codes), translated at endpoints via
  `result.Match(Results.Ok, CustomResults.Problem)`. The `DomainValidationException` path
  retires with ADR-0004.
- **Database lifecycle.** `dotnet ef` migrations replace `EnsureCreated()`; ADR-0002's
  deferral trigger is reached by this decision. When the first migration lands, existing
  dev databases are dropped and recreated (no `__EFMigrationsHistory` exists), and the
  deferrable vacancy-requirement position constraint is hand-edited into the migration
  `Up()` per the recorded migration notes.
- **Test mechanics.** Every slice carries handler unit tests (xUnit + Shouldly +
  NSubstitute over an in-memory `TestDbContext`), validator tests
  (`FluentValidation.TestHelper`), and HTTP-seam integration tests (xUnit +
  `WebApplicationFactory` + Testcontainers PostgreSQL). ADR-0003's flow-first scope and
  traceability naming still decide *what deserves a test*; the pack's three test types
  are *how a backend slice is tested*.

Unchanged: domain vocabulary (`CONTEXT.md`), the `/api` seam and ProblemDetails
responses, privacy/security rules, the frontend conventions, and the work order in
`docs/agents/workflow.md`.

## Consequences

- This is a documentation-level adoption. Existing code keeps its ADR-0004 shape until
  refactored; new slices follow the pack. Mixed conventions in the tree are expected
  during the transition and are not review findings.
- `docs/agents/dotnet.md` defers to the pack for slice mechanics and keeps only
  repo-specific rules (glossary authority, API boundary, EF/PostgreSQL guidance, privacy,
  performance stance).
- MediatR remains excluded; the handler shape is the owned-abstraction fallback ADR-0004
  sanctioned.
