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

## 3. Gap analysis: this repo TODAY vs. these articles

### Already matches

| Article guidance | Repo state (post-ADR-0004) |
|---|---|
| Feature folders; everything for a use case in one place (2.1) | `Features/Vacancies/`, `Features/Candidates/` with contracts, use cases, endpoints co-located |
| File-per-use-case with nested types is fine; "start with one file, split into a folder when uncomfortable" (2.1, 2.4 "flatten it when the feature count is small") | Exactly the repo rule: `<UseCase>.cs` one file, subfolder only for deep work (`Features/Candidates/Import/`) |
| No MediatR needed; direct handler invocation, "no mediator, no runtime indirection" (2.3) | ADR-0004 direct static handlers — one step *lighter* than even 2.3's interface-based setup |
| Informal command/query split: reads and writes as separate slices (round 1 Art. 1; 2.4) | Read slices (`ListVacancies`, `GetVacancy`, `GetCvDocument`) vs. write slices (`CreateVacancy`, `UpdateVacancy`, `CloseVacancy`, `ReopenVacancy`, `PurgeVacancy`, `ImportCandidates`) — the articles bless exactly this lightweight shape; 2.4's flattening example is the repo's layout |
| Tier 1: infrastructure shared by default (2.2) | `AppDbContext`, EF configuration, storage under `Infrastructure/` |
| Tier 2: push business rules into the domain; slices share the domain model (2.2) | `Domain/Vacancies/`, `Domain/Candidates/` with invariant-enforcing entities; all slices share them |
| Tier 3: feature-family sharing stays inside the feature (2.2) | `VacancyWrite.cs` (transactional write policy) and `VacancyProgress.cs` (details-with-progress, 4 consumers) at the feature root, named for concepts — the repo's `<Concept>.cs` is the same idea with a better name than `Shared/`, which 2.2 explicitly invites ("feel free to find a better name than Shared") |
| Cross-cutting behavior gets one home (2.2 Tier 1 / "Shared — cross-cutting behaviors only") | `Features/Shared/DomainValidationExceptionHandler.cs` — the single HTTP adapter for the domain-exception policy |
| Commands return the created resource rather than re-querying (2.4's eventual-consistency mitigation) | `CreateVacancy` returns `Created` with the full `VacancyDetailsResponse`; `UpdateVacancy` returns the updated details |
| Anti-pattern warnings: no `Common` junk drawer, no shared cross-feature service layer (2.2) | No `Common`/`Utils`/`Services` folder exists; the only cross-feature feature code is the single HTTP adapter above, and architecture tests block slice-to-slice dependencies |

### Deliberate differences, with verdicts

| Article pattern | Repo position | Verdict |
|---|---|---|
| Rule of Three: "don't abstract until you hit three" (2.2) | AGENTS.md: shared code is earned at its **second** same-reason consumer | **Aligned in spirit, stricter in threshold.** Both reject abstract-on-first-use. The repo extracts one consumer earlier, justified because its extractions are *policies with one home* (transaction+lock policy in `VacancyWrite`, progress-composition in `VacancyProgress`), not look-alike code — and 2.2's own question 2 ("how stable is this concept?") supports early extraction for stable policies. No change warranted; see recommendation 1 for a docs note. |
| Thin `ICommand`/`IQuery`/`ICommandHandler` interfaces + Scrutor decorators (2.3) | No interfaces, no DI dispatch: plain static handler calls (ADR-0004) | **Justified.** 2.3's own reasons for interfaces are (a) structuring by intent and (b) enabling decorators for logging/validation. The repo gets (a) from file-per-use-case naming and (b) from the framework: .NET 10 `AddValidation()` is the validation pipeline, and ASP.NET Core already logs requests. ADR-0004's bar — "a dispatch abstraction needs a concrete policy or lifetime boundary to earn its place" — is unmet. 2.3 is the sanctioned fallback if that changes (recommendation 2). |
| Dapper/raw SQL for query slices (2.4) | Reads use EF Core projections (`VacancyProgress.ProjectSummaries`, `AsNoTracking`) | **Justified at current scale.** 2.4 says "each slice picks the tool that fits" and "start simple." `docs/agents/dotnet.md` allows parameterized SQL where suitable and treats query micro-optimization as measured hot-path work. The repo already has the *seam* (queries are separate slices projecting directly to response DTOs), so adopting Dapper later is a per-slice change, not a refactor. |
| `Result`/`Result<T>` returns (2.3, 2.4) | `DomainValidationException` + `DomainValidationExceptionHandler` | **Fine (unchanged from round 1).** 2.3 marks the Result wrapper "optional"; the repo's convention produces the same RFC 7807 boundary. |
| Reflection/Scrutor endpoint+handler auto-registration (2.1 steps 2–3) | Explicit `MapVacancyEndpoints`/`MapCandidateEndpoints` in `Program.cs` | **Justified.** Two features don't need scanning; explicit registration keeps the composition root legible and the architecture tests already enforce the surface. Revisit only when feature count makes registration noisy. |
| Carter modules (2.4) | Plain route groups in `*Endpoints.cs` | **Justified.** Carter would add a dependency for what `MapGroup` already does; its validation selling point is obsolete on .NET 10 (verified fact #3). |

### Specific placement check requested: `VacancyWrite` / `VacancyWriteQueries`

- `Features/Vacancies/VacancyWrite.cs` (transaction + locked mutation policy, consumed by
  `UpdateVacancy`, `CloseVacancy`, `ReopenVacancy`, `PurgeVacancy`) → **matches 2.2 Tier 3**:
  feature-family sharing, named for its concept, lives inside the feature. ✅
- `Infrastructure/Vacancies/VacancyWriteQueries.cs` (`SELECT ... FOR UPDATE` extension on
  `AppDbContext`, enforces the transaction precondition) → **matches 2.2 Tier 1 + the repo's own
  rule** ("persistence details remain in `Infrastructure/` unless the feature module owns the
  complete persistence operation"). 2.2 says infrastructure is shared by default and each slice
  owns its *data access* — here the data access (the lock query) is a single feature-scoped
  persistence helper, not a cross-feature service; placing it under `Infrastructure/Vacancies/`
  keeps EF/SQL concerns out of the feature while the *policy* (`VacancyWrite`) stays in the
  feature. Placement is consistent with the article; no move indicated.

### Genuine gaps

1. **Docs vocabulary lag, not code.** The repo rule ("second same-reason consumer") and 2.2's
   Rule of Three differ on paper; neither `AGENTS.md` nor `docs/agents/dotnet.md` cites the
   three-tier taxonomy that would explain why `VacancyWrite`/`VacancyProgress`/`Features/Shared`
   are placed where they are. A reader following only 2.2 might expect a `Shared/` subfolder per
   feature or wait for three consumers. (Doc-level only.)
2. **No recorded fallback for the dispatch decision.** ADR-0004 rejects MediatR/FluentValidation
   but doesn't name what *would* earn a dispatch abstraction. 2.3 provides the precise answer
   (owned interfaces + decorators, triggered by a cross-cutting concern the framework doesn't
   cover, e.g. per-command authorization or idempotency). Worth one paragraph so a future
   contributor adds 2.3's shape instead of reaching for MediatR.
3. **Nothing else.** On the two structural questions this round asked — where shared logic lives,
   and whether the informal command/query split should be formalized — the repo already matches
   the articles' guidance. The articles *bless* the lightweight shape: 2.4's flattened layout and
   "start simple" are the repo's design, and 2.3 sets the only formalization bar (interfaces for
   decoration) that the framework already covers for this codebase.

---

## 4. Recommended improvements, prioritized

Effort: S (< half day), M (1–2 days), L (> 2 days).

1. **[S] Add the three-tier sharing vocabulary to `docs/agents/dotnet.md` (and the AGENTS.md
   contract line).** Cite 2.2's tiers (infrastructure shared by default / push to domain /
   feature-family stays local) next to the existing `<Concept>.cs` and `Features/Shared` rules,
   and note the deliberate deviation from the Rule of Three (extract stable *policies* at the
   second consumer; look-alike *code* still waits). Why: closes gap 1; makes the existing
   `VacancyWrite`/`VacancyProgress` placements explainable from first principles.
2. **[S] Amend ADR-0004 (or add a short note in `docs/agents/dotnet.md`) naming 2.3's shape as
   the sanctioned fallback.** If a cross-cutting concern ever outgrows framework hooks (endpoint
   filters, `AddValidation`), the answer is owned `ICommand`/`IQuery` interfaces + decorators
   (Scrutor optional) — not MediatR. Why: 2.3 is the primary-source endorsement of exactly this
   ladder; recording it prevents re-litigating toward a commercially licensed library.
3. **Nothing else prioritized.** Given gaps 1–2 are documentation-only, the honest finding is:
   **the repo already matches these four articles; only docs/rule tweaks remain.** No code work
   is recommended from this round.

### Recommendations NOT to adopt, and why

- **Do not introduce MediatR, including 2.4's `ISender`/`IPipelineBehavior` examples.** 2.3
  (same author, written *because* MediatR went commercial) is the newer and more deliberate
  guidance; 2.4's MediatR idioms are illustrative shorthand it explicitly caveats ("CQRS is not
  MediatR"). ADR-0004 stands. (github.com/LuckyPennySoftware/MediatR)
- **Do not add the thin `ICommandHandler`/`IQueryHandler` interfaces + Scrutor now.** 2.3's
  interfaces exist to enable decorators; both decorator use cases (validation, logging) are
  already covered by the .NET 10 validation pipeline and framework logging. Adding them today is
  ceremony without a consumer — the exact "thin abstractions" pattern `docs/agents/dotnet.md`
  warns against. It's the documented fallback (recommendation 2), not a to-do.
- **Do not add reflection-based endpoint auto-registration or Scrutor scanning (2.1 steps 2–3).**
  Two feature groups don't justify startup reflection; explicit `*Endpoints.cs` registration
  keeps the composition root legible, and `SliceArchitectureTests` already enforce the public
  surface that scanning would conventionally produce.
- **Do not adopt Carter (2.4).** It duplicates `MapGroup` + minimal-API features this repo
  already uses, and its validation selling point is obsolete on .NET 10 (`AddValidation()`;
  verified fact #3).
- **Do not introduce Dapper query slices or a separate read store (2.4) as a standing task.** 2.4
  itself says "start with a single database. Split when performance demands it." EF projections
  with `AsNoTracking` meet current needs; the slice seam makes this a future per-slice decision,
  gated on measurement per `docs/agents/dotnet.md`.
- **Do not split shared response contracts per use case** (strictest reading of 2.2 rule 1 /
  2.4's DTO-sharing caveat). The rule targets *cross-feature* ownership and command/query pairs
  that merely "look the same"; `VacancyDetailsResponse` is one feature's canonical representation
  with genuinely identical shape across its use cases, and `VacancyProgress` exists precisely
  because its composition is shared deliberately. If a use case's response ever diverges, split
  then — 2.2's duplication example is the trigger to watch for, not a current violation.

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
9. `docs/adr/0004-direct-handlers-and-input-validation.md`
10. `docs/agents/dotnet.md`, `AGENTS.md`
11. `hr-sat.Server/Features/Vacancies/` (endpoints, contracts, `VacancyWrite`, `VacancyProgress`,
    use cases), `hr-sat.Server/Features/Candidates/`, `hr-sat.Server/Features/Shared/`,
    `hr-sat.Server/Infrastructure/Vacancies/VacancyWriteQueries.cs`,
    `hr-sat.Server.Tests/Architecture/SliceArchitectureTests.cs`
