# Research findings: Milan Jovanović's Vertical Slice Architecture articles

**Date:** 2026-08-31
**Scope:** Four Milan Jovanović articles on Vertical Slice Architecture (VSA), verified against
primary sources where they make claims about external APIs (.NET 10, MediatR, FluentValidation),
then mapped against this repository (`hr-sat.Server`, ASP.NET Core net10.0, EF Core + Npgsql,
minimal APIs).

All four blog URLs fetched successfully on 2026-08-31. No fetch failures. One caveat: the fourth
URL turned out to be an index/hub page, not a technical article (details in its section).

---

## 1. Per-article summary

### 1.1 Vertical Slice Architecture (Nov 4, 2023)

URL: https://milanjovanovic.tech/blog/vertical-slice-architecture

- Layered architectures (N-tier, Clean Architecture) give low coupling *between* layers but high
  coupling *inside* a layer, and force a feature change to touch many layers (domain model,
  validation, MediatR use case, controller). Cohesion per feature is low.
- VSA inverts this: "Minimize coupling between slices, and maximize coupling in a slice." All
  files for one use case live in one folder, so cohesion per use case is high.
- Splitting requests into commands (POST/PUT/DELETE) and queries (GET) gives CQRS benefits
  without ceremony. Each slice may pick its own data access: one slice uses EF Core, another may
  use Dapper/raw SQL.
- "New features only add code, you're not changing shared code and worrying about side effects."
- The acknowledged cost: because a use case contains most of its own logic, you must spot code
  smells and refactor by pushing logic down to the domain when a slice grows too large.
- Presents the **REPR pattern** (Request-EndPoint-Response) as the slice shape for APIs, and
  notes it "can be achieved with the MediatR library, **for example**" — MediatR is illustrative,
  not mandatory. FastEndpoints and Ardalis.ApiEndpoints are listed as alternatives.
- Example solution structure: `Features/<Feature>/<UseCase>/` containing Request, Endpoint,
  Query/Command, Handler, Validator files.

### 1.2 Vertical Slice Architecture: Structuring Vertical Slices (Jun 1, 2024)

URL: https://milanjovanovic.tech/blog/vertical-slice-architecture-structuring-vertical-slices

- A slice is a self-contained unit cutting through the whole stack: endpoint → validation → data
  access. Benefits claimed: improved cohesion, reduced complexity, focus on business logic, easier
  maintenance (changes localized to the slice).
- Recommended slice shape in .NET: a **static class per feature** grouping `Request`, `Response`,
  `Validator`, and an `Endpoint` class (implementing an `IEndpoint` interface with
  `MapEndpoint(IEndpointRouteBuilder)`). The use case logic can live directly in a Minimal API
  endpoint handler.
- The example handler injects `AppDbContext` **directly into the endpoint** — no MediatR, no
  repository. The article explicitly acknowledges the trade-off ("might tightly couple the slice
  to your database technology") and says a repository abstraction is *optional*, "depending on
  your project's size and requirements."
- Validation: a `Validator` class (FluentValidation `AbstractValidator<Request>`) defined *inside
  the slice*, injected into the endpoint via DI, called manually in the handler in this article.
  Validators support DI for complex rules.
- Handling complexity and shared logic: decompose complex features into smaller slices; refactor
  with extract-method/extract-class; extract genuinely shared logic into a shared class used from
  slices; and "push logic down" — start procedural (Transaction Script), then move logic that
  belongs to the domain into domain entities.

### 1.3 Validation in Vertical Slice Architecture (Aug 13, 2026)

URL: https://milanjovanovic.tech/blog/validation-vertical-slice-architecture

- Validators live **next to the handler** (same file as command + handler), never in a
  project-wide `Validators` folder.
- Recommended setup: FluentValidation validators + a **MediatR pipeline behavior**
  (`ValidationBehavior<TRequest, TResponse>`) that runs all matching `IValidator<T>`
  automatically before the handler; registered via `AddMediatR(cfg => cfg.AddOpenBehavior(...))`
  and `AddValidatorsFromAssembly(...)`.
- Two variants shown: throw `ValidationException` (paired with a global exception handler
  producing 400 Problem Details) or return a `Result` / `ValidationError` (with the caveat that
  generic `Result<T>` failure construction needs a factory or reflection). "Pick one convention
  and stay consistent."
- **Input validation vs domain validation** split: input validation (format, presence — "is the
  email format valid?") belongs in the validator and runs *before* the handler; domain validation
  (business rules — "is this product in stock?") belongs in the handler or domain model.
- Async validators hitting the database are possible but should be used sparingly: "Save database
  checks for the handler when possible."
- Co-located validators are trivially unit-testable with FluentValidation's `TestValidate` helper.
- Endpoint error mapping: map validation errors to RFC 7807 Problem Details
  (`Results.ValidationProblem` with errors grouped into a dictionary).

### 1.4 Vertical Slice Architecture in .NET: The Complete Guide (Jul 10, 2026)

URL: https://milanjovanovic.tech/blog/vertical-slice-architecture-dotnet

**Caveat: this page is a curated index/hub, not a technical article.** It contains a short
definition ("each feature contains everything it needs - the request, the handler, the
validation, and the data access - in one place... adding a new feature means adding a new slice,
not modifying five different layers") and then link-lists to other articles. Relevant links it
surfaces: shared logic placement, Carter-based slices, cross-cutting concerns, and
**architecture tests** (NetArchTest-style) to enforce slice boundaries — a topic not covered by
the other three articles. It makes no new claims to verify.

---

## 2. Verified .NET 10 facts (blog claims checked against primary sources)

| # | Claim (from blog or implied by its examples) | Verified status for .NET 10 | Primary source |
|---|---|---|---|
| 1 | The REPR pattern "can be achieved with MediatR" (Art. 1); the validation article builds entirely on MediatR pipeline behaviors (Art. 3). | **Still technically true, but commercially changed.** MediatR has moved to the `LuckyPennySoftware` GitHub org and now requires a **license key** (v13+ lineage; latest release v14.2.0), configured via `cfg.LicenseKey` or `MEDIATR_LICENSE_KEY` / `LUCKYPENNY_LICENSE_KEY` env vars, obtained at mediatr.io. It is no longer the free, dependency-light default the 2023–2024 articles assume. | github.com/LuckyPennySoftware/MediatR (README, releases) |
| 2 | Minimal API handlers can inject `AppDbContext` and services directly via parameter injection (Art. 2 example). | **Holds.** Route handler parameters are resolved from DI when the type is a registered service; `CancellationToken`, `HttpContext`, etc. bind as special types. Route groups (`MapGroup`) and extension-method registration outside Program.cs are documented, standard patterns. | learn.microsoft.com/aspnet/core/fundamentals/minimal-apis (aspnetcore-10.0) |
| 3 | Validation must come from a library (FluentValidation) or manual checks (Arts. 2, 3). | **Outdated as the only option.** .NET 10 ships **built-in Minimal API validation**: `builder.Services.AddValidation()` registers a source-generated validation pipeline; an endpoint filter validates query/header/body parameters annotated with `System.ComponentModel.DataAnnotations` attributes (on classes *and* records), custom `ValidationAttribute`s, and `IValidatableObject`. Failures return 400 with error details automatically. Opt out per-endpoint with `.DisableValidation()`; customize error output via `IProblemDetailsService`. The top-level `AddValidation` API and validation filter are **stable**; only the underlying resolver APIs are marked experimental. Validation APIs moved to the `Microsoft.Extensions.Validation` package/namespace (old references redirect). | learn.microsoft.com/aspnet/core/release-notes/aspnetcore-10.0; learn.microsoft.com/aspnet/core/fundamentals/minimal-apis ("Validation support in Minimal APIs") |
| 4 | Map validation errors to Problem Details, e.g. `Results.ValidationProblem(dictionary)` (Art. 3). | **Holds.** `Results.ValidationProblem` and `Results.Problem` are built-in result types; `IProblemDetailsService` integration is the .NET 10-blessed customization point. | learn.microsoft.com/aspnet/core/fundamentals/minimal-apis (aspnetcore-10.0) |
| 5 | (Implicit in all minimal-API guidance) return-type handling for endpoints. | **Confirmed with a stronger recommendation:** Microsoft docs state "`TypedResults` is preferred to `Results`" for testability and OpenAPI metadata. This repo already follows it (`Results<Created<...>, ValidationProblem>`). | learn.microsoft.com/aspnet/core/fundamentals/minimal-apis (aspnetcore-10.0) |
| 6 | (Testing slices via WebApplicationFactory — repo practice, related to blog's testing links.) | **.NET 10 improvement:** a source generator now emits `public partial class Program` automatically for top-level-statement apps, and an analyzer *advises removing* an explicit declaration. This repo declares `public partial class Program;` explicitly in `Program.cs` — now redundant. | learn.microsoft.com/aspnet/core/release-notes/aspnetcore-10.0 ("Better support for testing apps with top-level statements") |

**Net answer on MediatR/FluentValidation for this repo:** neither is required by the blog's own
core argument. Article 1 presents MediatR as one way to get REPR; Article 2's canonical example
has no MediatR at all (endpoint + injected DbContext). Only Article 3 leans on MediatR, and only
for the *automatic validation pipeline* — a cross-cutting concern that minimal APIs can implement
with an `AddEndpointFilter` or, in .NET 10, with the built-in `AddValidation()` pipeline. Given
MediatR's commercial licensing (fact #1), not adopting it is doubly justified.

---

## 3. Gap analysis: this repo vs. the blog's recommendations

### What the repo already does that matches the blog

| Blog recommendation (source) | Repo implementation |
|---|---|
| Group all files of a use case in one folder; feature-first structure (Art. 1) | `Features/Vacancies/` with one file per use case (`CreateVacancy.cs`, `CloseVacancy.cs`, …); slice layout contract codified in `docs/agents/dotnet.md` |
| Endpoint logic in minimal API handlers with direct `AppDbContext` injection; no mandatory repository (Art. 2) | `CreateVacancy.HandleAsync(request, AppDbContext, CancellationToken)` in `hr-sat.Server/Features/Vacancies/CreateVacancy.cs` — exactly the blog's shape |
| Route groups + extension-method endpoint registration outside Program.cs (Art. 2's `IEndpoint.MapEndpoint`; Microsoft docs) | `VacancyEndpoints.MapVacancyEndpoints()` uses `MapGroup("/api/vacancies")`; `Program.cs` stays a pure composition root |
| Command/query split per slice = CQRS for free (Art. 1) | Reads (`ListVacancies`, `GetVacancy`, `GetCvDocument`) are separate slices from writes (`CreateVacancy`, `CloseVacancy`, …) |
| Push logic down to domain when slices grow (Art. 2); domain validation enforces business rules in the domain model (Art. 3) | Rich domain entities (`Domain/Vacancies/Vacancy.cs`) with `VacancyValidationException`; `Vacancy.Create(...)` enforces invariants |
| Map errors to RFC 7807 Problem Details via a global handler (Art. 3) | `AddProblemDetails()` + `DomainValidationExceptionHandler` (`Features/Shared/DomainValidationExceptionHandler.cs`) + `app.UseExceptionHandler()` in `Program.cs` |
| Feature-shared behavior stays at feature root; shared logic extracted only when real (Art. 2, "Extract shared logic") | `VacancyWrite.cs` (transactional write concept) and `VacancyProgress.cs` at the feature root; `Features/Shared/` holds the cross-feature HTTP adapter |
| Slices testable via the HTTP seam (Art. 1.4's testing links; repo ADR-0003) | `hr-sat.Server.Tests` with `ApiFactory` (WebApplicationFactory + Testcontainers) |

### Deliberate differences (fine per the blog)

| Blog pattern | Repo position | Verdict |
|---|---|---|
| MediatR commands/handlers + `ISender` in endpoints (Arts. 1, 3) | No MediatR; static handler methods called directly by endpoint registration | **Justified.** Art. 1 calls MediatR one option "for example"; Art. 2's example omits it; `docs/agents/dotnet.md` explicitly prefers deep modules over thin abstractions; MediatR is now commercially licensed (fact #1) |
| FluentValidation + MediatR `ValidationBehavior` pipeline (Art. 3) | No FluentValidation; input validation currently implicit via nullable request fields flowing into domain validation | **Partially justified** — no need for the MediatR pipeline, but see gap below: input validation as a distinct boundary is missing |
| `Result` pattern returned from handlers (Art. 3) | Domain exceptions translated by `DomainValidationExceptionHandler` | **Fine.** Art. 3: "Throw or return Results - both work, pick one convention and stay consistent." The repo picked exceptions consistently |
| One folder per use case (`GetActivity/`, `CreateActivity/`) (Art. 1) | One *file* per use case; subfolder only when deep (Candidates `Import/`) | **Equivalent or better** for this codebase size; avoids folder-per-type noise |

### Genuine gaps for this .NET 10 codebase

1. **No input-validation boundary at the API edge.** `VacancyDefinitionRequest` uses nullable
   fields (`string? Title`) and lets everything fall through to domain validation. Per Art. 3's
   input-vs-domain split, presence/format checks belong *before* the handler. .NET 10's built-in
   `AddValidation()` + DataAnnotations (fact #3) would give this without new dependencies — the
   blog's stated reason for FluentValidation (automatic, co-located input validation) is now
   covered by the framework.
2. **Redundant `public partial class Program;`** in `Program.cs` — .NET 10 generates it via
   source generator and ships an analyzer advising removal (fact #6).
3. **No OpenAPI metadata on endpoints.** The slices map bare routes (no `.WithTags()`,
   `.WithName()`, `.Produces<>()`). The blog's examples use `.WithTags(...)`; Microsoft docs note
   endpoint names feed OpenAPI operation IDs. The repo already serves Scalar, so richer metadata
   is immediately visible value.
4. **No architecture enforcement.** The slice layout contract in `docs/agents/dotnet.md` is
   convention-only. Art. 1.4 links Milan's architecture-tests articles (NetArchTest-style) as the
   VSA quality gate; none exist here.
5. **Shared-kernel rule implicit, not tested.** "Minimize coupling between slices" (Art. 1) is
   enforced by review only — e.g., nothing stops a `Candidates` slice from depending on
   `Vacancies` internals.

---

## 4. Recommended improvements, prioritized

Effort: S (< half day), M (1–2 days), L (> 2 days).

1. **[S] Remove the explicit `public partial class Program;` declaration** from
   `hr-sat.Server/Program.cs`. .NET 10's source generator emits it and an analyzer flags the
   manual declaration (fact #6). Zero risk; the `ApiFactory` keeps working.
2. **[S/M] Adopt .NET 10 built-in input validation for write endpoints.** Add
   `builder.Services.AddValidation()` in `Program.cs`; annotate `VacancyDefinitionRequest` (and
   future write contracts in `Features/*/…Contracts.cs`) with DataAnnotations
   (`[Required]`, length limits) so obviously-bad input is rejected with a 400 *before* domain
   logic runs — the exact input-vs-domain split of Art. 3, without adding FluentValidation or
   MediatR. Verify the generated 400 shape matches the repo's RFC 7807 conventions
   (`IProblemDetailsService` is the customization seam if not).
3. **[S] Add OpenAPI endpoint metadata.** `.WithTags("Vacancies")` on the group in
   `VacancyEndpoints.cs` / `CandidateEndpoints.cs`, plus `.WithName(...)` per route (names become
   operation IDs) — matches the blog's examples and improves the already-hosted Scalar docs.
4. **[M] Add a small architecture-test suite** (e.g. NetArchTest.Rules) in
   `hr-sat.Server.Tests` enforcing the documented slice contract: feature slices don't reference
   each other, `Domain/` doesn't reference `Features/`/`Infrastructure/`, endpoints stay in
   `*Endpoints.cs`. This is the quality gate Art. 1.4 points to and turns
   `docs/agents/dotnet.md` conventions into executable checks.
5. **[S] Document the "no MediatR, no FluentValidation" decision in an ADR.** The research
   record (this file) shows the blog treats both as optional and that MediatR is now commercially
   licensed; a short ADR (`docs/adr/0004-…`) prevents re-litigating and guides future
   contributors/agents.

### Blog recommendations that should NOT be adopted

- **Do not introduce MediatR** to get the Art. 3 validation pipeline. It contradicts
  `docs/agents/dotnet.md` ("prefer a deep, cohesive module over a collection of thin
  abstractions"), the blog itself shows MediatR-free slices as the baseline (Art. 2), and MediatR
  now requires a commercial license key (github.com/LuckyPennySoftware/MediatR). The same
  automatic-validation outcome is available via `AddValidation()` (.NET 10) or one shared
  `AddEndpointFilter` if custom rule engines ever become necessary.
- **Do not adopt FluentValidation as the default.** Art. 3's argument for it is co-located,
  automatic input validation; .NET 10's built-in pipeline covers the repo's current needs
  (presence/format on small request records) with no new dependency. Revisit only if rule
  complexity genuinely outgrows DataAnnotations + `IValidatableObject`.
- **Do not switch to the `Result`-pattern** for domain failures. Art. 3 explicitly blesses either
  convention; the repo's `DomainValidationException` + `DomainValidationExceptionHandler` already
  produces consistent Problem Details, and a switch would churn every slice for no behavioral
  gain.
- **Do not restructure to one folder per use case** (Art. 1's example layout). The repo's
  file-per-use-case with subfolders only for deep slices (e.g. `Features/Candidates/Import/`) is
  an intentional, documented refinement; folders-per-type would add navigation noise at this
  codebase's size.

---

## 5. Sources

Primary (blog):

1. https://milanjovanovic.tech/blog/vertical-slice-architecture (Nov 4, 2023)
2. https://milanjovanovic.tech/blog/vertical-slice-architecture-structuring-vertical-slices (Jun 1, 2024)
3. https://milanjovanovic.tech/blog/validation-vertical-slice-architecture (Aug 13, 2026)
4. https://milanjovanovic.tech/blog/vertical-slice-architecture-dotnet (Jul 10, 2026 — index/hub page)

Verification (external primary sources):

5. https://learn.microsoft.com/en-us/aspnet/core/release-notes/aspnetcore-10.0 — .NET 10 Minimal API
   validation (`AddValidation`, `DisableValidation`, `IProblemDetailsService`, stability note),
   `Microsoft.Extensions.Validation` move, source-generated `public partial class Program`.
6. https://learn.microsoft.com/en-us/aspnet/core/fundamentals/minimal-apis?view=aspnetcore-10.0 —
   parameter/DI binding, route groups, validation in Minimal APIs, `TypedResults` preference,
   `Results.ValidationProblem`.
7. https://github.com/LuckyPennySoftware/MediatR — MediatR's current home, commercial license-key
   requirement, latest release v14.2.0.
