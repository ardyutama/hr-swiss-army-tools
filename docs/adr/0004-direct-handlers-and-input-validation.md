# ADR 0004: Direct handlers and framework input validation

## Status

Accepted (2026-08-31)

## Context

Backend slices call use-case handlers directly from endpoint registrations, with no
mediator, and validation used to live entirely in the domain. Research against Milan
Jovanović's vertical-slice articles
(`docs/research/2026-08-31-milan-jovanovic-vsa-findings.md`) checked this shape against
the patterns those articles recommend:

- The articles present MediatR as optional ("for example"); their canonical minimal-API
  example injects `AppDbContext` straight into the endpoint. Since the articles were
  written, MediatR has become a commercially licensed library (LuckyPennySoftware).
- The articles split validation into input validation (presence/format, before the
  handler) and domain validation (business rules, in the domain). This repo had only the
  domain half: `VacancyDefinitionRequest` used nullable fields and let everything fall
  through to `Vacancy.Create`.
- .NET 10 ships a built-in minimal-API validation pipeline (`AddValidation()` +
  DataAnnotations), which covers the co-located, automatic input validation the articles
  previously needed FluentValidation plus a MediatR pipeline behavior for.

## Decision

- Endpoint registrations invoke slice handlers directly; dispatch stays a plain static
  call. A dispatch abstraction needs a concrete policy or lifetime boundary to earn its
  place, per `docs/agents/dotnet.md`. If a cross-cutting concern ever outgrows framework
  hooks (endpoint filters, `AddValidation`), the sanctioned shape is owned
  `ICommand`/`IQuery` handler interfaces with decorators (Scrutor optional), per
  milanjovanovic.tech/blog/cqrs-pattern-the-way-it-should-have-been-from-the-start —
  never MediatR, which is commercially licensed.
- The input-validation boundary is the .NET 10 framework pipeline: `AddValidation()` in
  `Program.cs`, with DataAnnotations presence rules (`[Required]`, `[MinLength]`) on
  write request contracts. Rules that depend on normalization or state (trim, case,
  uniqueness, lifecycle) stay in the domain and keep their stable client-visible error
  keys and messages.
- The domain-validation exception convention stays: `DomainValidationException` +
  `DomainValidationExceptionHandler` remains the single path from business-rule failure
  to RFC 7807 response, in place of a Result-pattern rewrite.
- The slice layout contract is executable: `hr-sat.Server.Tests/Architecture/` enforces
  slice isolation, domain purity, and the public surface of `Features/`.

## Consequences

- Missing required input is rejected with a 400 before the handler runs. Framework error
  keys arrive PascalCase and are normalized client-side (`shared/validation.ts`
  lowercases keys), so the client contract is unchanged.
- New write contracts carry DataAnnotations presence rules as part of the slice.
- MediatR or FluentValidation enter the stack only by superseding this ADR.
