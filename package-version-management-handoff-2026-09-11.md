# Handoff - Package version management follow-up (2026-09-11)

## Session goal
Implement candidate 1 from the package version architecture review, then leave a
focused handoff for candidates 2 and 3.

## What was done
- Added the root `Directory.Packages.props` module with
  `ManagePackageVersionsCentrally=true` and all 20 package versions currently used by
  the backend and test projects.
- Removed inline `Version` attributes from the four project manifests that own package
  references.
- Preserved every existing version, including `Microsoft.AspNetCore.Mvc.Testing`
  `10.0.0` and `Microsoft.AspNetCore.OpenApi` `10.0.10`; no packages were upgraded or
  deleted.
- Kept the client `package.json` and its NuGet adapter unchanged.
- Removed the redundant direct `Microsoft.OpenApi` reference from the Web API project.

## Validation
- `dotnet restore tests/hr-sat.Tests/hr-sat.Tests.csproj`: passed; the existing
  transitive `SQLitePCLRaw.lib.e_sqlite3` NU1903 advisory remains.
- `dotnet build tests/hr-sat.Tests/hr-sat.Tests.csproj --no-restore`: passed.
- Architecture tests: 4 passed.
- `npm run type-check` from `src/hr-sat.Client`: passed.
- Manifest audit: no inline `PackageReference` versions remain.
- The full backend test project discovered 116 tests; 52 passed and 64 could not run
  because Docker is unavailable at `npipe://./pipe/docker_engine` for Testcontainers.
- Candidate 3 restore and Web API build pass without warnings; the final graph shows
  `Microsoft.OpenApi 2.7.5` as a transitive package.

## Follow-up decision
### Candidate 2 - shared Microsoft train properties
No shared Microsoft train properties were added. The central package list already keeps
the versions together, while aliases such as `$(EntityFrameworkCoreVersion)` would add
another interface without reducing meaningful drift.

The existing `Microsoft.AspNetCore.Mvc.Testing` `10.0.0` versus
`Microsoft.AspNetCore.OpenApi` `10.0.10` difference remains unchanged. No alignment was
made without an explicit version-policy decision and compatibility check.

### Candidate 3 - remove redundant `Microsoft.OpenApi`
`Microsoft.AspNetCore.OpenApi` remains and owns the current `AddOpenApi` and `MapOpenApi`
calls. NuGet initially resolved the transitive package to `2.0.0` after the direct
reference was removed, so `CentralPackageTransitivePinningEnabled` and the existing
central `Microsoft.OpenApi` version were retained to preserve `2.7.5` without restoring
a direct project reference.