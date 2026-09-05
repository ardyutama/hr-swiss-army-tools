# Kernel design: adopt, omit, or deviate

Type: grilling
Status: open
Blocked by: 01

## Question

For every type in the kernel inventory (ticket 01), decide: adopt as minimal
hand-rolled code, omit, or deviate — and where it lives (`hr-sat.Domain` /
`hr-sat.Application` abstractions / `hr-sat.Infrastructure` / `hr-sat.Web.Api`, or a
separate SharedKernel project).

Known decision points:

- Keep BCL `TimeProvider` instead of the pack's `IDateTimeProvider` (recommended —
  already registered in `Program.cs`).
- No `IUserContext` and no `.RequireAuthorization()`: this LAN MVP has no auth
  (recommended omit; record as an explicit security posture, not an oversight).
- `ValidationDecorator` behavior: collect all validator failures before the handler;
  how its 400 body maps to the current `ValidationProblem` dictionary so the seam
  stays byte-stable (PascalCase keys; the client lowercases).
- `IApplicationDbContext` surface: `DbSet`s + `SaveChangesAsync`, and how raw SQL,
  `ExecuteDeleteAsync`, and `FindVacancyForUpdateAsync` (FOR UPDATE) reach the
  database through the interface.
- `Entity` base + `IDomainEvent` + dispatch-on-save: machinery only, no events raised
  yet (settled in charting, Q8a).
- `Result`/`Result<T>` + `Error` + `{Entity}Errors` code conventions
  (`"{Feature}.{Reason}"`); `CustomResults.Problem` and `Tags` homes in Web.Api.

Each deviation from the pack needs a recorded rationale; any that prove hard to
reverse earn an ADR.

## Comments
