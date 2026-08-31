# Research findings (round 2): Milan Jovanović on feature folders, shared logic, and CQRS

**Date:** 2026-08-31
**Scope:** Four more Milan Jovanović articles — feature folders, shared-logic placement, and two
CQRS articles — verified against primary sources where they make claims about external libraries,
then mapped against the current repository state (`hr-sat.Server`, ASP.NET Core net10.0, EF Core +
Npgsql, minimal APIs, post-ADR-0004).

**This is round 2.** Round 1 (`docs/research/2026-08-31-milan-jovanovic-vsa-findings.md`) covered
the VSA intro, structuring-slices, validation articles, and the VSA index page; its decisions are
implemented as ADR-0004 (`docs/adr/0004-direct-handlers-and-input-validation.md`). This file
extends round 1; it does not repeat its findings. Where round-2 articles contradict or refine
round-1 articles, that is called out explicitly.

All four blog URLs fetched successfully on 2026-08-31. No fetch failures.

---

## 1. Per-article summary

### 2.1 Feature Folders in .NET (Aug 13, 2026)

URL: https://milanjovanovic.tech/blog/feature-folders-dotnet

- Organizing by layer (`Controllers/`, `Services/`, `Models/`) means one feature change touches
  5+ folders; feature folders flip this so everything for one feature lives together
  (`Features/Orders/PlaceOrder/` with endpoint, command, handler, request, response, validator
  files). Feature folders are how VSA and Screaming Architecture naturally organize code.
- The example slice uses the thin `ICommand`/`ICommandHandler` abstractions from the CQRS article
  (2.3 below), FluentValidation validators, and a `Result` return — explicitly linked, not
  re-argued.
- Two registration conveniences are shown: **reflection-based endpoint auto-registration** (scan
  the assembly for static `Map(IEndpointRouteBuilder)` methods and invoke them) and
  **Scrutor assembly scanning** for handler DI registration ("so a new slice never means editing
  Program.cs").
- **One file vs. one folder per operation: both are explicitly fine.** The single-file style
  (static class with nested `Command`, `Validator`, `Handler`, `Map` types) is "the style I lean
  toward for small-to-medium slices." Guidance: "Start with one file. Split into a folder when the
  file gets uncomfortable to scroll."
- Shared code: put genuinely shared code in a `Common`/`Shared` folder but "keep the shared folder
  minimal. If code is only used by one feature, it belongs in that feature's folder... extract
  when the duplication hurts, not when it merely exists" (deferring to article 2.2).
- Migration is incremental: create `Features/` next to existing layers, move one feature end to
  end, coexist, repeat opportunistically. "A consistent structure that hides features is worse
  than a mixed one that's converging on clarity."
- **Refines round 1:** round 1's structuring article (Art. 1.2) showed folder-per-use-case; this
  article explicitly blesses file-per-use-case, which is exactly this repo's refinement
  (`docs/agents/dotnet.md`: "Start a use case as one file. Give it a subfolder when its
  implementation has multiple cohesive modules"). The repo's `Features/Candidates/Import/`
  subfolder matches the "split when uncomfortable" trigger.

### 2.2 Vertical Slice Architecture: Where Does the Shared Logic Live? (Nov 29, 2025)

URL: https://milanjovanovic.tech/blog/vertical-slice-architecture-where-does-the-shared-logic-live

- A generic `Common`/`Utils` project is "almost always a mistake" — it becomes a junk drawer
  coupling unrelated features with different change frequencies through the same helpers,
  reintroducing the coupling VSA was meant to escape.
- **Decision framework (three questions):** (1) infrastructural or domain? (2) how stable is the
  concept? (3) are you past the **Rule of Three** — "Duplicating the same code once is fine...
  Don't abstract until you hit three."
- **Three tiers of sharing:**
  - *Tier 1 — Technical infrastructure (share freely):* logging, DB contexts, auth middleware,
    Result pattern, validation pipelines → `Shared.Kernel`/`Infrastructure`.
  - *Tier 2 — Domain concepts (share and push logic down):* business rules belong on entities and
    value objects; "different vertical slices can share the same domain model."
  - *Tier 3 — Feature-specific logic (keep it local):* logic shared by related slices lives in a
    `Shared/` folder *inside the feature* (`Features/Orders/Shared/`); deleting the feature
    deletes its shared logic — no zombie code.
- **Cross-feature sharing:** first ask whether you need to share at all — "most cross-feature
  'sharing' is just data access in disguise." Each slice owns its data access and queries the
  database directly rather than calling into another feature; the shared *entity* lives in
  `Domain`. Genuine cross-feature logic goes to `Domain/Services` (business rules) or
  `Infrastructure/Services` (technical). Side effects in another feature → messaging/events or a
  feature facade.
- **Duplication is sometimes right:** two identical-looking DTOs (`GetOrderResponse` /
  `CreateOrderResponse`) will diverge; "duplication is cheaper than the wrong abstraction."
- **The Rules (verbatim list):** (1) features own their request/response models, no exceptions;
  (2) push business logic into the domain; (3) keep feature-family sharing local; (4)
  infrastructure is shared by default; (5) apply the Rule of Three — three real usages with
  identical, stable logic.
- **Refines round 1:** round 1's Art. 1/1.2 said "push logic down to the domain when a slice grows
  too large" and "extract genuinely shared logic." This article supplies the missing placement
  taxonomy (three tiers + `Domain/Services` for cross-feature domain logic) and a stricter
  extraction threshold (Rule of Three vs. round 1's unspecified "when real").

### 2.3 CQRS Pattern the Way It Should've Been From the Start (May 17, 2025)

URL: https://milanjovanovic.tech/blog/cqrs-pattern-the-way-it-should-have-been-from-the-start

- **Written explicitly in response to MediatR going commercial** (links Jimmy Bogard's
  announcement; FAQ: "Why did .NET teams start looking for MediatR alternatives in 2025?"). This
  directly confirms round 1's verified fact #1 and strengthens ADR-0004's premise.
- Core claim: "You don't need MediatR to implement CQRS." MediatR became synonymous with CQRS
  despite "CQRS and MediatR are not the same thing"; most projects use it as a thin dispatching
  layer replaceable by a few interfaces.
- Proposed shape: marker interfaces `ICommand`/`ICommand<TResponse>`/`IQuery<TResponse>` plus
  `ICommandHandler<...>`/`IQueryHandler<...>` returning `Result`/`Result<TResponse>` (Result
  wrapper is explicitly optional). "No mediator, no runtime indirection... The handler is invoked
  directly" — interfaces exist to structure logic around *intent* (reads vs. writes) and to enable
  decoration.
- Cross-cutting concerns (logging, validation) are added as **decorators** (technically proxies)
  around the generic handler interfaces, registered with **Scrutor** (`services.Scan(...)` +
  `services.Decorate(...)`); decorator order: last applied = outermost.
- Endpoints inject the specific `ICommandHandler<TCommand>` directly — "No need for `ISender`, no
  mediator layer, no runtime lookup."
- **Changes the round-1 MediatR story:** round 1 showed MediatR was optional in the 2023–2024
  articles; this 2025 article actively recommends *replacing* it with owned abstractions. Note the
  floor it sets: even Milan's MediatR-free setup keeps handler *interfaces* + DI dispatch, which is
  one step more ceremony than this repo's direct static handler calls (ADR-0004).

### 2.4 Combining Vertical Slices With CQRS in .NET (Aug 13, 2026)

URL: https://milanjovanovic.tech/blog/combining-vertical-slices-cqrs

- CQRS decides how reads/writes are modeled; VSA decides where code lives. Combined, "each command
  and query becomes an independent slice with its own data access strategy" — commands via EF Core
  with change tracking, queries via Dapper/raw SQL for performance.
- Folder shapes: `Features/Orders/Commands|Queries/` subfolders, **or flatten when the feature
  count is small** (`PlaceOrder.cs`, `GetOrder.cs` side by side) — again blessing the repo's flat
  file-per-use-case shape.
- Code examples still use MediatR-style `IRequest`/`ISender` (with the explicit caveat "CQRS is
  not MediatR") and **Carter** modules (`ICarterModule`) for endpoint registration.
- Typed `ICommand`/`IQuery` marker interfaces over `IRequest` let pipeline behaviors target
  commands-only (validation) or queries-only (caching) selectively.
- Read/write models are separate: rich write-side domain entity vs. flat read-side DTOs "optimized
  for the UI." Scaling path: start with one database; split into a denormalized read store or
  replica only "when performance demands it."
- **Two honest caveats:** eventual consistency creeps in as soon as queries read from a
  cache/replica (mitigate by returning the created resource from the command instead of
  re-querying); and "slice independence is a discipline, not a guarantee" — resist sharing DTOs
  between a command and its neighboring query "because they look the same"; extract shared logic
  deliberately per article 2.2.
- **Tension with 2.3 worth noting:** this 2026 article's examples still lean on MediatR idioms
  (`ISender`, `IPipelineBehavior`) while 2.3 argues for owned interfaces — the consistent core
  across both is *read/write separation and per-slice data access*, not any dispatch library.

---

## 2. Verified facts (external claims checked against primary sources)

| # | Claim (from the articles) | Verified status (2026-08-31) | Primary source |
|---|---|---|---|
| 1 | MediatR is going/has gone commercial — the trigger for article 2.3 (2.3; also referenced in feature-folders FAQ) | **Confirmed.** Already verified in round 1 (fact #1): MediatR lives at LuckyPennySoftware/MediatR and requires a commercial license key (latest v14.2.0). Jimmy Bogard's announcement post is the article's own cited source. | github.com/LuckyPennySoftware/MediatR; jimmybogard.com/automapper-and-mediatr-going-commercial |
| 2 | Scrutor provides `IServiceCollection.Scan(...)` assembly scanning and `Decorate(...)` for the decorator pipeline (2.1 step 3, 2.3 DI setup) | **Confirmed.** Scrutor (Kristian Hellang, MIT license) adds exactly `Scan` and `Decorate` extensions to `IServiceCollection`; open-generic scanning (`typeof(IQueryHandler<,>)`) is a documented example. Latest release v7.0.0 (~Nov 2025); repo shows .NET 10 upgrades 9 months ago; actively maintained, 12K dependent repos. Free — no licensing concern. | github.com/khellang/Scrutor (README, releases) |
| 3 | Carter modules (`ICarterModule.AddRoutes`) as the endpoint-registration mechanism (2.4) | **Confirmed as a real, maintained library, with a caveat.** Carter (CarterCommunity, MIT) is a thin layer over ASP.NET Core minimal APIs; active (commits last month, .NET 10 support). Caveat: its README claims FluentValidation-based request validation "is not available with ASP.NET Core Minimal APIs" — **outdated for .NET 10**, which ships `AddValidation()` (round 1, fact #3). One more reason this repo doesn't need Carter for validation. | github.com/CarterCommunity/Carter (README); learn.microsoft.com/aspnet/core/release-notes/aspnetcore-10.0 |
| 4 | Reflection-based endpoint auto-registration and handler scanning are safe/standard (2.1 steps 2–3) | **Technically fine, deliberately skipped here.** Startup-time reflection over the entry assembly works and Scrutor's README documents the exact patterns. This repo's contract instead keeps `Program.cs` an explicit composition root with `*Endpoints.cs` registration — a documented choice in `docs/agents/dotnet.md`, and the architecture tests already enforce the surface that scanning would conventionally imply. | github.com/khellang/Scrutor; docs/agents/dotnet.md |
| 5 | No new .NET 10 runtime/API claims | The four articles are pattern-level; the only framework-specific surface touched (minimal APIs, endpoint filters, DI) was already verified for .NET 10 in round 1. Nothing new to verify. | — |

---

## 5. Sources

Primary (blog, all fetched 2026-08-31):

1. https://milanjovanovic.tech/blog/feature-folders-dotnet (Aug 13, 2026)
2. https://milanjovanovic.tech/blog/vertical-slice-architecture-where-does-the-shared-logic-live (Nov 29, 2025)
3. https://milanjovanovic.tech/blog/cqrs-pattern-the-way-it-should-have-been-from-the-start (May 17, 2025)
4. https://milanjovanovic.tech/blog/combining-vertical-slices-cqrs (Aug 13, 2026)

Verification (external primary sources):

5. https://github.com/khellang/Scrutor — Scrutor README/releases: `Scan`/`Decorate` extensions,
   open-generic scanning examples, MIT license, v7.0.0 latest, .NET 10 support, active maintenance.
6. https://github.com/CarterCommunity/Carter — Carter README: `ICarterModule`, minimal-API
   layering, MIT license, active maintenance; validation claim noted as outdated for .NET 10.
7. https://github.com/LuckyPennySoftware/MediatR — MediatR commercial licensing (carried over
   from round 1 verification; corroborates article 2.3's premise).

Repository documents cross-referenced:

8. `docs/research/2026-08-31-milan-jovanovic-vsa-findings.md` (round 1)
