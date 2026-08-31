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
