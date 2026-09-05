# .NET Architecture and Coding

Use this guide for backend feature work, domain-model changes, EF Core queries and
configuration, ASP.NET Core endpoints, dependency injection, backend tests, or .NET
performance work.

## Authority and context

Read `CONTEXT.md` and the relevant ADRs before changing a domain term or an architectural
boundary. Backend slice conventions — layers, slice layout, handlers, validation, error
handling, and per-slice tests — are owned by the skill pack under
`.agents/skills/vertical-slice-dotnet/` (`add-feature`, `add-entity`, `add-tests`,
`ca-review`), which is the source of truth per ADR-0005. This guide keeps only the
repository-specific rules the pack does not cover. When guidance conflicts, use this
order:

1. The current feature specification and `CONTEXT.md`.
2. Accepted ADRs, especially `docs/adr/0002-mvp-stack-and-api-seam.md` and
   `docs/adr/0005-clean-architecture-template-conventions.md`.
3. The `vertical-slice-dotnet` skill pack (slice conventions).
4. This guide and `docs/agents/workflow.md` (repo rules and work order).
5. A matching playbook under `.agents/skills/dotnet-skills/`.

The codebase predates the pack: existing slices still follow the superseded ADR-0004
conventions (`internal static` `HandleAsync`, `DomainValidationException`,
`AddValidation()`, `EnsureCreated()`). Migrate a slice's conventions when a ticket
touches it and build new slices on the pack; the mixed tree is expected during the
transition and is not a review finding.

Use the vocabulary in `CONTEXT.md` in code, tests, issues, and documentation. A new
domain term belongs in the glossary before it becomes a public name.

## Architecture

The backend is an ASP.NET Core monolith on the Clean Architecture template's layers with
vertical-slice use cases (full conventions: the `add-feature` and `add-entity` skills):

```text
src/Domain/            entities, {Entity}Errors catalogs, domain events; references
                       SharedKernel only
src/Application/       one folder per use case under {Feature}/{UseCase}/: command or
                       query, handler, validator, response DTO; data access only through
                       IApplicationDbContext
src/Infrastructure/    AppDbContext, EF configuration, migrations, persistence helpers
src/Web.Api/           composition root plus minimal-API endpoints mirrored per use case
                       at Endpoints/{Feature}/{UseCase}.cs
```

### Naming and placement

- Keep product project names in the `hr-sat.{Module}` form, including `hr-sat.Client`.
- Place product projects under `src/` and test projects under `tests/`.
- Name backend feature folders with plural domain nouns from `CONTEXT.md`, use-case folders `{Verb}{Entity}`, and endpoint files under `Endpoints/{Feature}/{UseCase}/`.

Within a slice folder, keep **one type per file**, each file named exactly after the type
it holds (`UpdateVacancyCommand.cs`, `UpdateVacancyCommandValidator.cs`,
`UpdateVacancyCommandHandler.cs`). The command/query record, its validator, and the
handler are separate responsibilities — they never share a file.

Handlers are `internal sealed` with primary constructors, implementing the owned
`ICommandHandler<>`/`IQueryHandler<>` abstractions and discovered by Scrutor assembly
scanning with decorators — never MediatR, and never manual DI registration for handlers,
validators, or endpoints. Expected failures return `Result`/`Result<T>` from
`{Entity}Errors` factories; endpoints translate them with
`result.Match(Results.Ok, CustomResults.Problem)`. Input validation is a FluentValidation
`{Command}Validator` per command, run by the `ValidationDecorator` before the handler.

Prefer a deep, cohesive module over a collection of thin abstractions. Add a repository,
provider, factory, or service interface only when a concrete policy, external boundary,
lifetime boundary, or independently replaceable behavior requires it. EF Core does not
need a repository wrapper by default. Keep `Program.cs` focused on composition,
middleware, infrastructure registration, and application startup; feature endpoint
mapping belongs in the feature's endpoint modules.

Complete a feature as a vertical slice: schema and persistence, API behavior, client
feature, and the tests for the declared seams. Follow the implementation-before-tests
work order in `docs/agents/workflow.md`; generic TDD or generated-test playbooks do not
override that project decision.

### Sharing across slices

Slices stay independent: each owns its DTOs and queries the database itself rather than
calling a sibling slice — most cross-feature "sharing" is data access in disguise. Extract
shared code only when earned (second same-reason consumer; third for merely look-alike
code — the repo's refinement of the Rule of Three) and place it by tier: technical
infrastructure in `SharedKernel`/`Infrastructure`, business rules on domain types or in
Domain services, feature-family helpers in a `Shared/` folder inside the feature. The full
taxonomy lives in the pack at `add-feature/references/shared-logic.md`; a generic
`Common`/`Utils` home is a junk drawer, not a tier.

### Building a slice

Follow the pack's skills directly: `add-feature` scaffolds a use case end to end
(command/query, handler, validator, endpoint, and the three test types), `add-entity`
wires a new domain entity through every layer including the migration, `add-tests`
backfills coverage on an existing use case, and `ca-review` checks pending changes
against the conventions. The templates under `add-feature/references/` are the canonical
file shapes; finish with `dotnet build` and `dotnet test` green.

## Domain model

- Put business invariants and state transitions in domain types, not only in endpoint
  branches or EF configuration.
- Keep invalid state difficult to construct. Validate constructor and mutation inputs at
  the domain boundary; expected failures surface as `Result` errors from the feature's
  `{Entity}Errors` catalog, and commands that mutate state raise a domain event via
  `entity.Raise(...)` before saving.
- Entities are `sealed`, inherit the `SharedKernel` `Entity` base, and hold foreign-key
  ids rather than navigation properties; relationships are configured shadow-style in the
  EF configuration.
- Keep persistence concerns in `Infrastructure/`; do not make database shape the domain
  model unless the domain genuinely requires that shape.
- Treat `Vacancy`, `Vacancy Requirement`, `Vacancy Status`, and the other terms in
  `CONTEXT.md` as scoped domain concepts. Do not silently introduce global candidate or
  shared-requirement concepts that contradict the glossary.
- Preserve ordering, ownership, and lifecycle rules in the model and database together.
  A database constraint is a backstop, not a substitute for a meaningful domain error.
- Map domain state to API contracts explicitly. Do not expose EF entities as an accidental
  public response model.

## API boundary

- Keep endpoints under `/api/*`.
- Return plain JSON on success; use RFC 7807 `ProblemDetails` for errors, translated only
  through `result.Match(Results.Ok | NoContent, CustomResults.Problem)`. Do not add a
  response envelope without an accepted design decision.
- Let commands answer with the created or updated resource (or its id) so clients use the
  command's response instead of re-querying.
- Validate external input at two boundaries: presence and format rules live in the
  command's FluentValidation validator and run before the handler via the
  `ValidationDecorator`; rules that depend on normalization or state (trim, uniqueness,
  lifecycle) stay in the domain. Translate expected validation failures into stable
  client-visible problem responses.
- Tag every endpoint group with `WithTags(...)` and name every route with
  `WithName(...)`; route names become the OpenAPI operation IDs in the hosted Scalar docs.
- Pass the request `CancellationToken` through asynchronous database and I/O operations.
- Keep endpoint handlers small and feature-local. They should coordinate validation,
  domain behavior, persistence, and the response rather than become a second domain
  model.
- For future uploads, validate size, declared content type, file signature, and generated
  storage names. Never turn a client-provided filename into a storage path.
- If cookie authentication is introduced for form endpoints, retain antiforgery
  protection. Make any API-specific exemption an explicit security decision.

## EF Core and PostgreSQL

Handlers reach data through `IApplicationDbContext`; the Npgsql-backed `AppDbContext`
implements it in Infrastructure and is the single registered context. For read paths:

- Filter and order in SQL before materialization.
- Project directly to the response contract or a feature-local read model.
- Use `AsNoTracking()` for queries that do not update entities.
- Materialize asynchronously with the request cancellation token.
- Use `AnyAsync()` for existence checks instead of counting rows.
- Avoid N+1 access; shape one query or use explicitly justified includes.
- Use `ExecuteUpdateAsync()` or `ExecuteDeleteAsync()` for suitable set-based writes.
- Use LINQ or parameterized SQL. Never concatenate SQL from external input.

Treat compiled queries and other query micro-optimizations as measured hot-path tools,
not defaults. Apply query guidance to PostgreSQL/Npgsql rather than copying provider-
specific SQL Server examples from a playbook.

Schema changes ship as `dotnet ef` migrations — ADR-0005 ends the `EnsureCreated()`
deferral from ADR-0002. Migration names are `PascalCase_With_Underscores`. When the first
migration lands, replace `EnsureCreated()` in `Program.cs` with `Database.Migrate()`, drop
and recreate existing dev databases, and hand-edit the migration `Up()` for the deferrable
vacancy-requirement position constraint (raw SQL; the fluent API cannot express it).

## Dependency injection and resources

- Prefer constructor injection or minimal API parameter injection.
- Handlers, validators, and endpoints are discovered by assembly scanning (`Scrutor`,
  `AddValidatorsFromAssembly`, `AddEndpoints`) — a new slice needs no manual registration.
- Keep `AppDbContext` scoped and never cache it beyond its unit of work.
- Register stateless, thread-safe application-wide services as singletons only when their
  state and dependencies support that lifetime.
- Use scoped lifetimes for request and unit-of-work behavior; use transient lifetimes for
  genuinely short-lived services where that improves ownership clarity.
- Use `IHttpClientFactory` if the application gains outbound HTTP integrations.
- Dispose resources through their owning scope and make ownership explicit for streams,
  files, and database work.

## Privacy and security

Candidate data, source emails, CV documents, and sender details are sensitive. Use
structured logs with stable identifiers and operational facts; do not log raw email
content, CV content, credentials, tokens, or unnecessary personal data. Keep secrets in
configuration mechanisms intended for secrets, never in source or committed settings.

Treat every request, uploaded file, and database query as an input boundary. Prefer
allowlists, parameterized access, generated storage names, and explicit authorization
checks when those features are added.

## Testing

Scope is flow-first per `docs/agents/testing.md` (ADR 0003): every test traces to a user
story or a `CONTEXT.md` term, and spine stories land their Flow Tests in the same ticket.
Mechanics follow the pack (ADR-0005): each slice carries handler unit tests (xUnit +
Shouldly + NSubstitute over an in-memory `TestDbContext`, substituting interfaces only),
validator tests (`FluentValidation.TestHelper`), and HTTP-seam integration tests (xUnit +
`WebApplicationFactory` + Testcontainers PostgreSQL). Name tests
`Handle_Should_{Outcome}_When{Condition}` (unit) or after their user story / domain rule
(integration), keep Arrange/Act/Assert, and assert exact errors, persisted state, and
raised domain events. The architecture-test suite enforces layer dependencies and slice
isolation; extend it when the contract grows. Assert observable behavior:

- status codes and response contracts;
- RFC 7807 error details for invalid or unavailable operations;
- domain state transitions and lifecycle rules;
- persistence effects and relationships; and
- cancellation or boundary behavior when it is part of the feature contract.

Keep tests isolated and deterministic. Avoid private-method tests, swallowed exceptions,
assertion-free tests, broad exception assertions, timing sleeps, test-order dependencies,
and exact collaborator-call verification unless the call itself is the behavior. Use
data-driven tests when cases genuinely share behavior and remain readable. Treat code
coverage as a diagnostic rather than a quality target by itself.

## Performance

Start with a workload and a measurement when performance is the problem. Prefer changes
to algorithmic complexity, database query shape, I/O, and allocation volume over isolated
micro-optimizations. Record the comparison and validate the result in the target
environment. Do not add `ValueTask`, `ConfigureAwait(false)`, compiled EF queries,
sealing, SIMD, or specialized collections as blanket rules.

## Conditional playbooks

For slice work the `vertical-slice-dotnet` pack is the primary playbook, not a
conditional one. Reach for the wider skill bundle only when its scope matches the task.
Useful entry points include:

- [ASP.NET file uploads](../../.agents/skills/dotnet-skills/dotnet-aspnet/skills/minimal-api-file-upload/SKILL.md)
  for upload-specific validation and antiforgery decisions.
- [EF Core query optimization](../../.agents/skills/dotnet-skills/dotnet-data/skills/optimizing-ef-core-queries/SKILL.md)
  for a measured query investigation.
- [.NET performance analysis](../../.agents/skills/dotnet-skills/dotnet-diag/skills/analyzing-dotnet-performance/SKILL.md)
  or [microbenchmarking](../../.agents/skills/dotnet-skills/dotnet-diag/skills/microbenchmarking/SKILL.md)
  for measured performance work.
- [test anti-patterns](../../.agents/skills/dotnet-skills/dotnet-test/skills/test-anti-patterns/SKILL.md)
  and [test maintainability](../../.agents/skills/dotnet-skills/dotnet-experimental/skills/exp-test-maintainability/SKILL.md)
  when reviewing or improving tests.
- [OpenTelemetry configuration](../../.agents/skills/dotnet-skills/dotnet-aspnet/skills/configuring-opentelemetry-dotnet/SKILL.md)
  only when observability is an accepted feature or operational requirement.

Playbooks that prescribe MSTest, repository wrappers over EF Core, mandatory
localization/XML documentation, or TDD are guidance for other project shapes. They are
not repository standards. MAUI, AI, native AOT, MSBuild,
publishing, and migration playbooks are similarly task-specific unless the project first
adopts that capability.

## Completion check

Before considering backend work complete, verify that every modified path has an owner in
the layer/slice structure, every new domain term uses the glossary, every command has a
FluentValidation validator and every expected failure an `{Entity}Errors` factory, every
state change raises its domain event, every async I/O path propagates cancellation, every
read query has an intentional tracking and projection shape, and the slice's unit,
validator, and HTTP-seam tests assert the observable contract. Run the `ca-review` skill
over the diff before committing.