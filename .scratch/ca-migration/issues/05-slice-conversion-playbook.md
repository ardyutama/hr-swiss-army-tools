# Per-slice conversion playbook (CreateVacancy reference)

Type: grilling
Status: open
Blocked by: 02, 04

## Question

Write the mechanical recipe each of the 11 use-case conversions follows, using
CreateVacancy as the worked reference:

- File moves: `Features/<Feature>/<UseCase>.cs` → `Application/{Feature}/{UseCase}/`
  + endpoint → `Web.Api/Endpoints/{Feature}/{UseCase}.cs`.
- `internal static HandleAsync` → `internal sealed` handler class with primary
  constructor implementing `ICommandHandler<>`/`IQueryHandler<>`.
- DataAnnotations → FluentValidation `{Command}Validator`. First establish what the
  existing integration tests assert about validation bodies (keys only vs exact
  message strings) so the seam stays byte-stable.
- `DomainValidationException` → `Result` + `{Entity}Errors` catalogs: the exact error
  codes for Vacancy and Candidate.
- Endpoint groups → per-use-case `IEndpoint`, preserving `WithTags`/`WithName` so
  route names (OpenAPI operation ids) don't drift.
- Folding `VacancyContracts`, `VacancyProgress`, `VacancyWrite` into slices per the
  sharing taxonomy (`add-feature/references/shared-logic.md`).
- Expressing the locked-mutation pattern (transaction + FOR UPDATE + mutate + save)
  and post-commit cleanup (`CancellationToken.None`) in handler form.
- The three-test-type checklist per slice and the green gate: `dotnet build` +
  full suite + `ca-review` pass.

## Comments
