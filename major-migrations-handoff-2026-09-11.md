# Handoff - Deferred major-version migrations (2026-09-11)

## Context

The 2026-09-11 dependency sweep bumped all 20 centrally-pinned NuGet packages to
latest stable: the Microsoft train aligned to 10.0.12, `Microsoft.NET.Test.Sdk`
to 18.10.0, `Testcontainers.PostgreSql` to 4.15.0, `Scalar.AspNetCore` to
2.17.3, and the `Microsoft.OpenApi` transitive pin to 2.12.0. Validation: restore,
build, 116/116 tests (Docker available), client type-check, and an OpenAPI
smoke check all green. Policy recorded in `docs/adr/0012-dependency-version-policy.md`.

Three majors were deferred per that policy. Each section below is a planning
prompt for a fresh agent: read it, gather the evidence listed, then propose a
migration plan. **Do not start the migrations in a sweep session.**

## 1. Microsoft.OpenApi 2.x -> 3.x

- Current: 2.12.0 (central pin, transitive-only; no direct project reference).
- Latest stable: 3.10.2.
- Why it matters: v3 rewrote the OpenAPI document model API (breaking).
  `Microsoft.AspNetCore.OpenApi` 10.0.12 currently requires `>= 2.12.0 && < 3.0.0`,
  so this migration is **blocked until the ASP.NET Core train itself moves to
  OpenApi 3.x** (expected with .NET 11 — verify at planning time).
- Evidence to gather: which `Microsoft.AspNetCore.OpenApi` version lifts the
  `< 3.0.0` cap; whether `Program.cs` (`AddOpenApi`/`MapOpenApi`) and Scalar
  need changes; diff the generated `/openapi/v1.json` before/after; check
  whether the central transitive pin can finally be dropped.

## 2. NSubstitute 5.x -> 6.x

- Current: 5.3.0. Latest stable: 6.2.0.
- Evidence to gather: the 6.0 changelog/breaking changes (arg matchers and
  received-call assertions are the usual suspects); grep the test project for
  `Arg.`, `Received(`, `Returns(` usage patterns and estimate the touch count;
  run the full suite after the bump — NSubstitute breaks at runtime, not
  compile time.

## 3. xunit.runner.visualstudio 3.x -> 4.x

- Current: 3.1.5. Latest stable: 4.0.0.
- Why it may matter: v4 aligns with xunit v3 tooling; the repo pins xunit 2.9.3
  (v2 line). Verify v4 still supports running xunit **v2** tests before
  bumping, or plan it together with a future xunit 2->3 migration.
- Evidence to gather: the runner v4 compatibility matrix; whether `dotnet test`
  and IDE discovery both still work with xunit 2.9.3; note that
  `Microsoft.NET.Test.Sdk` 18.10.0 is already in place.

## Execution outcome (2026-09-11)

- `NSubstitute` migrated from 5.3.0 to 6.2.0. The existing `Arg.Any` and `Returns`
  setups required no source changes; the focused CV document handler tests passed,
  and the full backend suite passed with 116 tests succeeding.
- `xunit.runner.visualstudio` migrated from 3.1.5 to 4.0.0 while `xunit` remains
  at 2.9.3. Restore and the focused xUnit test run passed, confirming discovery
  with the existing xUnit v2 tests; the full suite also passed.
- `Microsoft.OpenApi` remains at 2.12.0. `Microsoft.AspNetCore.OpenApi` 10.0.12
  requires `Microsoft.OpenApi` `>= 2.12.0 && < 3.0.0`, so the 3.x migration cannot
  restore on the current .NET 10 train. `Program.cs` and Scalar require no source
  changes until a stable ASP.NET Core train lifts that cap.

## Related, not a package major

- .NET 11 previews/RCs exist for all Microsoft packages and the Npgsql EF Core
  provider (11.0.0-preview.6). Upgrading the target framework is a separate
  ADR-worthy decision once .NET 11 GAs; keep the SDK pinned to 10.0.x in
  `global.json` until then.
