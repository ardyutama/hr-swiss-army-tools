# Inventory the kernel types the pack assumes

Type: research
Status: resolved
Blocked by: none

## Question

Enumerate every type the `.agents/skills/vertical-slice-dotnet/` pack assumes already
exists in the consuming repo. For each: its minimal shape (members and signatures),
which layer/project the pack puts it in, and which existing `hr-sat.Server` concept it
replaces or parallels.

Cover at least:

- SharedKernel primitives: `Result`/`Result<T>`, `Error` (and any `ValidationError`
  subtype), `Entity` base, `IDomainEvent`, domain-event dispatch.
- Application abstractions: `ICommand`/`ICommand<T>`/`IQuery<T>`,
  `ICommandHandler<>`/`IQueryHandler<>`, `IApplicationDbContext`.
- Cross-cutting machinery: `ValidationDecorator` (and any other decorators the pack
  registers via Scrutor), Scrutor scanning conventions, `CustomResults.Problem`,
  `Tags`, endpoint discovery (`IEndpoint`/`AddEndpoints`).
- Test infrastructure: `BaseHandlerTest`, `BaseIntegrationTest`, `TestDbContext`,
  `GlobalUsings`.
- Assumed services this repo deliberately lacks: `IUserContext`/authorization,
  `IDateTimeProvider` (we use BCL `TimeProvider`), `HybridCache`, password/token
  providers. Flag each as an adopt/omit/deviate candidate for the kernel-design ticket.

Sources: all four skills and their `references/` files under
`.agents/skills/vertical-slice-dotnet/`; the round-1/round-2 research docs
(`docs/research/2026-08-31-milan-jovanovic-*.md`); the current `hr-sat.Server` tree.

Findings land on throwaway branch `research/kernel-inventory` at
`docs/research/2026-08-31-ca-pack-kernel-inventory.md`.

## Answer

Resolved 2026-08-31 by research subagent. Findings: branch `research/kernel-inventory`,
commit `bb85386`, `docs/research/2026-08-31-ca-pack-kernel-inventory.md`.

~25 assumed types found: 7 SharedKernel primitives (`Result`/`Result<T>` with implicit
conversion + `Match`, `Error` with typed factories and status map, `ValidationError`,
`Entity` base with `DomainEvents`/`Raise` and Guid Id, `IDomainEvent`,
`IDomainEventHandler<T>`); 7 Application abstractions (messaging interfaces under
`Application.Abstractions.Messaging`, `IApplicationDbContext` under `Abstractions.Data`);
7 cross-cutting types (`ValidationDecorator` is the *only* decorator — caching and
authorization are in-handler in the pack; `CustomResults.Problem`, `Tags`,
`IEndpoint`/`AddEndpoints`, Scrutor scanning, FluentValidation registration); 4
test-infrastructure types (`BaseHandlerTest`, `TestDbContext`, `BaseIntegrationTest`/
`IntegrationTestWebAppFactory`, `GlobalUsings`). Seven assumed services this repo lacks
flagged omit/deviate (`IUserContext`, auth stack, `HybridCache`, password/token
providers → omit; `IDateTimeProvider` → deviate to BCL `TimeProvider` with
`DateTimeOffset`).

Surfaced for ticket 02: five types are referenced but never defined in the pack
(`ValidationError`, `ValidationDecorator`, the domain-event dispatcher,
`Permissions`/`HasPermission`, `AccessTokens`) — their shapes are design decisions, not
downloads; exact Scrutor `Scan`/`Decorate` call shapes come from the round-2 research
doc, not the pack; every layer is a namespace-mapping decision since the repo is
single-project; the pack assumes `Guid` Ids where this repo uses `long` identity
columns; `BaseIntegrationTest` assumes an auth stack that doesn't exist here.

## Comments
