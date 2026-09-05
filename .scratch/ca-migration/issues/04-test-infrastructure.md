# Test infrastructure for the pack's three test types

Type: grilling
Status: open
Blocked by: 02

## Question

Design the test infrastructure against the kernel decided in ticket 02:

- `TestDbContext` strategy: EF Core InMemory provider vs hand-written fake of
  `IApplicationDbContext` vs SQLite in-memory — given the hybrid constraint (charting
  Q5b): lock/SQL-heavy slices (UpdateVacancy, PurgeVacancy, DeleteCandidate,
  ImportCandidates) keep FOR UPDATE / `ExecuteDeleteAsync` / raw-SQL behavior at the
  Testcontainers seam only; in-memory unit tests cover guard and domain logic.
- `BaseHandlerTest` / `BaseIntegrationTest` shapes; how the existing `ApiFactory`
  (WebApplicationFactory + Testcontainers + respawn-style reset) maps onto
  `BaseIntegrationTest`.
- Package additions: Shouldly, NSubstitute, FluentValidation.TestHelper; xUnit stays.
- `GlobalUsings` for the test project.
- Test project layout: per-feature folders for new unit/validator tests while the 9
  grandfathered flat integration files port per slice and retire (charting Q6a).
- Which of the 11 use cases get in-memory unit tests vs integration-only coverage,
  and the per-slice porting checklist for the existing integration tests.

## Comments
