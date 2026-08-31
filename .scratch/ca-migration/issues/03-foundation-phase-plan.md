# Foundation phase plan: split, kernel, EF Initial

Type: grilling
Status: open
Blocked by: none

## Question

Decide the ordered foundation phases and their exact contents. Candidate phases
(recommended order: split → kernel → migration, each landing green):

1. **Mechanical four-project split** into `src/hr-sat.Domain`,
   `src/hr-sat.Application`, `src/hr-sat.Infrastructure`, `src/hr-sat.Web.Api`:
   namespaces, csproj reference graph, `hr-sat.slnx` update, the single test
   project's rename/location (`hr-sat.Tests`?), NetArchTest layer rules rewritten for
   cross-project boundaries, Docker/compose path updates. Zero behavior change; the
   existing integration suite is the guard (byte-stable seam, charting Q7a).
2. **Kernel + recomposition**: the kernel decided in ticket 02 lands on the split;
   Scrutor + FluentValidation package additions; `Program.cs` recomposition
   (assembly scanning, `AddValidatorsFromAssembly`, `AddEndpoints`); retirement of
   `DomainValidationExceptionHandler` — including what then renders unexpected
   exceptions as ProblemDetails.
3. **EF Initial migration**: `dotnet ef migrations add Initial`, hand-edit `Up()`
   with the deferrable vacancy-requirement position constraint (raw SQL, per repo
   memory `ef-migration-state.md`), `EnsureCreated()` → `Database.Migrate()`, dev
   databases dropped and recreated.

Also decide: does `hr-sat.Server.csproj` retire entirely (Web.Api inherits the
composition root and `wwwroot` client serving), and what happens to the
`hr-sat.client.esproj` F5 pairing.

## Comments
