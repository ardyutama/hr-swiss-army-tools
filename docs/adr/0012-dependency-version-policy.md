# Dependency version policy

NuGet package updates target the latest **stable** version only; prereleases
(including newer .NET previews/RCs) are excluded — a .NET version jump is a
framework decision, not a dependency bump. The Microsoft .NET servicing train
(`Microsoft.AspNetCore.*`, `Microsoft.EntityFrameworkCore*`,
`Microsoft.Extensions.*`) moves **as one aligned version**; per-package drift
within the train is accidental, not policy. New **major** versions are deferred
out of catch-up sweeps: each gets its own migration planning pass, triggered by
a handoff document, because a major is a code change with a breaking-change
surface, not a version-string edit. npm dependencies in `src/hr-sat.Client/`
are out of scope of this policy.

## Consequences

- Catch-up sweeps edit only `Directory.Packages.props` and validate with
  restore, build, the full test suite (Testcontainers included), the client
  type-check, and a smoke check for any runtime surface the sweep touches that
  tests do not cover.
- `Microsoft.OpenApi` is pinned for transitive resolution
  (`CentralPackageTransitivePinningEnabled`); its pin must stay at or above the
  floor required by `Microsoft.AspNetCore.OpenApi`, which moved it from 2.7.5
  to 2.12.0 when the train aligned to 10.0.12.
