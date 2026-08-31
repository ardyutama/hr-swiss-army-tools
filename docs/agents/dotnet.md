# .NET Architecture and Coding

Use this guide for backend feature work, domain-model changes, EF Core queries and
configuration, ASP.NET Core endpoints, dependency injection, backend tests, or .NET
performance work.

## Authority and context

Read `CONTEXT.md` and the relevant ADRs before changing a domain term or an architectural
boundary. This guide adapts the local .NET skill bundle to this repository; it does not
replace a task-specific playbook. When guidance conflicts, use this order:

1. The current feature specification and `CONTEXT.md`.
2. Accepted ADRs, especially `docs/adr/0002-mvp-stack-and-api-seam.md`.
3. This guide and `docs/agents/workflow.md`.
4. A matching playbook under `.agents/skills/dotnet-skills/`.

Use the vocabulary in `CONTEXT.md` in code, tests, issues, and documentation. A new
domain term belongs in the glossary before it becomes a public name.

## Architecture

The backend is an ASP.NET Core monolith using feature-local Vertical Slice Architecture
over the existing project structure:

```text
hr-sat.Server/
  Domain/<Context>/          domain entities, value concepts, and invariants
  Features/<Feature>/        contracts, handlers, queries, and endpoint mapping
  Infrastructure/            AppDbContext, EF configuration, and persistence helpers
  Program.cs                 composition root and application startup
```

Keep a feature's behavior close to its endpoint, contract, handler, and persistence
query. Add a new feature folder rather than placing feature behavior in a global
`Services`, `Repositories`, or `Handlers` folder. Keep `Program.cs` focused on
composition, middleware, infrastructure registration, and application startup; feature
endpoint mapping belongs in the feature slice.

Prefer a deep, cohesive module over a collection of thin abstractions. Add a command
handler, repository, provider, factory, or service interface only when a concrete policy,
external boundary, lifetime boundary, or independently replaceable behavior requires it.
EF Core does not need a repository wrapper by default.

Endpoint registrations invoke slice handlers directly; dispatch is a plain static call
with no mediator in between (ADR-0004). Input validation runs before the handler through
the .NET 10 pipeline: `AddValidation()` in `Program.cs` plus DataAnnotations presence
rules on write contracts.

### Slice layout contract

Within `Features/<Feature>/`, keep the slice sorted by ownership:

```text
<Feature>Endpoints.cs       route registration only
<Feature>Contracts.cs       contracts shared by the feature's use cases; request contracts
                            carry DataAnnotations presence rules
<UseCase>.cs                one endpoint use case
<UseCase>/                  only when that use case has deep internal work
  <UseCase>.cs              endpoint orchestration and public slice interface
  <internal modules>.cs     cohesive implementation details
<Concept>.cs                feature-shared behavior named after its concept
```

Start a use case as one file. Give it a subfolder when its implementation has multiple
cohesive modules with useful locality of their own, as with the candidate import
pipeline; the endpoint module remains the only public interface of that use case. Do
not create folders merely to mirror types, and do not move feature behavior into global
`Services`, `Handlers`, or `Repositories` folders.

Feature-shared modules belong at the feature root and are named for the domain concept
they own, such as `VacancyProgress`; cross-feature HTTP adapters belong under
`Features/Shared` and are named for the policy they adapt. A shared module needs two
real consumers or a policy that must have one home. Persistence details remain in
`Infrastructure/` unless the feature module owns the complete persistence operation.

Sharing follows three tiers (research round 2):

- **Infrastructure is shared by default.** `AppDbContext`, EF configuration, and storage
  live in `Infrastructure/` and any slice may use them; each slice still owns its data
  access, and slices never call into each other (enforced by
  `hr-sat.Server.Tests/Architecture/`).
- **Domain concepts are shared and deepened.** Business rules live on the entities in
  `Domain/`; every slice shares the same domain model.
- **Feature-family logic stays local.** Behavior shared by one feature's use cases lives
  at the feature root as a `<Concept>.cs`. Only cross-cutting policies get a cross-feature
  home in `Features/Shared/`.

The "earned at the second same-reason consumer" threshold deliberately deviates from the
Rule of Three: it applies to extracting a stable *policy* with one home (a transaction
policy, a cross-cutting adapter). Look-alike code still waits for its third consumer —
duplication is cheaper than the wrong abstraction.

New backend flow-test files mirror this shape as
`hr-sat.Server.Tests/<Feature>/<UseCase>Tests.cs` and continue to cross the HTTP seam.
The existing flat suites are grandfathered by ADR-0003 and are not moved solely to
make the folders look uniform.

Complete a feature as a vertical slice: schema and persistence, API behavior, client
feature, and the tests for the declared seams. Follow the implementation-before-tests
work order in `docs/agents/workflow.md`; generic TDD or generated-test playbooks do not
override that project decision.

## Domain model

- Put business invariants and state transitions in domain types, not only in endpoint
  branches or EF configuration.
- Keep invalid state difficult to construct. Validate constructor and mutation inputs at
  the domain boundary and use the repository's domain validation exception pattern where
  it applies.
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
- Return plain JSON on success; use RFC 7807 `ProblemDetails` for errors. Do not add a
  response envelope without an accepted design decision.
- Validate external input at two boundaries: presence rules (`[Required]`, `[MinLength]`)
  on write contracts run in the `AddValidation()` pipeline before the handler; rules that
  depend on normalization or state (trim, uniqueness, lifecycle) stay in the domain.
  Translate expected validation failures into stable client-visible problem responses.
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

Use the registered `AppDbContext` and Npgsql configuration. For read paths:

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

The current schema lifecycle deliberately uses `EnsureCreated()`. Do not introduce
migrations or replace it with `Migrate()` until the migration trigger in ADR-0002 is
reached. When that trigger is reached, follow the recorded migration notes, including the
manual treatment of the deferrable vacancy-requirement position constraint.

## Dependency injection and resources

- Prefer constructor injection or minimal API parameter injection.
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
The mechanics stay as they are: backend tests cross the HTTP seam with xUnit,
`WebApplicationFactory`, and a hermetic Testcontainers PostgreSQL database. The slice
layout contract is executable: `hr-sat.Server.Tests/Architecture/` enforces slice
isolation, domain purity, and the public surface of `Features/`; extend it when the
contract grows. Assert observable behavior:

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

Reach for the attached skill bundle only when its scope matches the task. Useful entry
points include:

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

Playbooks that prescribe MSTest, generic command-handler or repository layers, template
namespace conventions, mandatory localization/XML documentation, or TDD are guidance for
other project shapes. They are not repository standards. MAUI, AI, native AOT, MSBuild,
publishing, and migration playbooks are similarly task-specific unless the project first
adopts that capability.

## Completion check

Before considering backend work complete, verify that every modified path has an owner in
the feature/domain/infrastructure structure, every new domain term uses the glossary,
every external input has a validation boundary, every async I/O path propagates
cancellation, every read query has an intentional tracking and projection shape, and the
relevant HTTP-seam tests assert the observable contract.