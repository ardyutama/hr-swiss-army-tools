# Research findings: FusionCache — applicability and impact for hr-sat

**Date:** 2026-09-20
**Scope:** Whether FusionCache (ZiggyCreatures) is worth adopting for this repo's
ingestion-heavy, fetch-heavy ASP.NET Core + PostgreSQL app, and if so where and when.
Sources are primary only: the FusionCache GitHub repo README and docs/ pages
(CacheStampede, FailSafe, Timeouts, EagerRefresh, AdaptiveCaching, Tagging, CacheLevels,
Backplane, MicrosoftHybridCache), nuget.org package pages
(`ZiggyCreatures.FusionCache`, `Microsoft.Extensions.Caching.Postgres`), Microsoft Learn
(HybridCache and distributed-caching articles), and the Microsoft devblogs HybridCache GA
announcement. All URLs fetched successfully on 2026-09-20. No fetch failures. The
FusionCache docs pages DependencyInjection.md and NamedCaches.md were not fetched
directly; claims about DI and named caches are taken from the README, CacheLevels.md,
Backplane.md, and MicrosoftHybridCache.md, which quote the same setup APIs.

---

## 1. Repo context (what exists today)

Grounded in repo files, not external sources:

- **Zero caching anywhere.** No `IMemoryCache`, `IDistributedCache`, `HybridCache`,
  output caching, or response caching under `src/` (verified by search on 2026-09-20).
- **Read paths are EF Core / raw-SQL query handlers** in
  `src/hr-sat.Application/Features/`: `ListCandidatesQueryHandler` (the ADR-0016 seam,
  raw SQL via `PostgresCandidateListReader` in `src/hr-sat.Infrastructure`),
  `GetReviewQueueQueryHandler`, `GetCandidateDetailsQueryHandler` (with
  `CandidateDetailsReader` and `CandidatePriorApplicationResponse` — the prior-application
  email lookup), `GetMessagingSummaryQueryHandler`, `GetPromoteSummaryQueryHandler`,
  `ListVacanciesQueryHandler`, `GetVacancyQueryHandler`, `GetFormLayoutQueryHandler`,
  `GetScreeningRulesQueryHandler`, `PreviewScreeningRulesQueryHandler` (the live
  "would screen out N of M" count), plus EmailTemplates queries.
- **Deployment is self-hosted/on-prem, likely single-instance**, per
  [docs/research/2026-09-15-windows-desktop-onprem-deployment-findings.md](2026-09-15-windows-desktop-onprem-deployment-findings.md):
  a self-contained single-file Kestrel exe on one Windows machine (SQLite locally) or a
  single on-prem server with PostgreSQL 18 in Docker. No server farm, no Redis anywhere in
  the topology; [docker-compose.yml](../../docker-compose.yml) runs the app plus one
  PostgreSQL container.
- **Screening is deliberately live-computed** per
  [docs/adr/0014-screening-rules-as-computed-disposition-never-deletion.md](../adr/0014-screening-rules-as-computed-disposition-never-deletion.md):
  "re-evaluated live so reclassification is free" for active rounds; only at Round Closure
  is a per-candidate Screening Verdict frozen and stored (the ADR-0014 amendment). Any
  caching of active-round screening results works directly against this promise.
- **The candidate list read seam is already optimized** per
  [docs/adr/0016-candidate-list-read-seam-with-contract-tested-dual-adapters.md](../adr/0016-candidate-list-read-seam-with-contract-tested-dual-adapters.md):
  one raw-SQL read returns full page rows (all scalars, CV count, `ScreenedOut`, fired
  rules) plus counts; the handler does no re-fetch. Page size is 100.
- **Full-text requirement matching is V3 and in-memory in C#** per
  [docs/adr/0009-full-text-requirement-matching-with-hybrid-ocr-extraction.md](../adr/0009-full-text-requirement-matching-with-hybrid-ocr-extraction.md):
  extracted CV text lives in the database on the CV Document; matching is a case-insensitive
  word-boundary phrase match in memory — "no tsvector, no ILIKE."
- **Dependency policy** ([docs/adr/0012-dependency-version-policy.md](../adr/0012-dependency-version-policy.md)):
  latest stable only, no prereleases; central package management with
  `CentralPackageTransitivePinningEnabled` in
  [Directory.Packages.props](../../Directory.Packages.props). The Microsoft servicing train
  moves as one aligned version (currently 10.0.12). Current stack: .NET SDK 10.0.100
  ([global.json](../../global.json)), EF Core 10.0.12, Npgsql EF provider 10.0.3.

---

## 2. What FusionCache is

URL: https://github.com/ZiggyCreatures/FusionCache
URL: https://www.nuget.org/packages/ZiggyCreatures.FusionCache

- **Identity.** "FusionCache is an easy to use, fast and robust hybrid cache with advanced
  resiliency features" (README). Hybrid means it works transparently as a plain memory
  cache (L1) or as a two-level cache (L1 memory + L2 distributed over any
  `IDistributedCache` implementation). Author: Jody Donetti; first public commits ~2019
  ("Initial commit, 6 years ago" on the repo).
- **License: MIT** (repo badge and LICENSE.md; NuGet license link
  https://licenses.nuget.org/MIT).
- **Current stable: 2.8.0**, released ~2026-09-13 ("last week" per the repo's Releases
  panel on 2026-09-20; NuGet "Last updated 7 days ago"). 66 releases total.
- **Maintenance status: very active.** Latest commit on main was hours old at fetch time;
  CI updated within the last month; 51 contributors, 3.9k stars, 430 dependents on GitHub.
  NuGet shows 45.5M total downloads for the core package (~61.9K/day); the README claims
  100M+ across the FusionCache packages (author's own claim).
- **Target frameworks:** NuGet lists **.NET 8.0** and **.NET Standard 2.0** assets for
  2.8.0; the README says "FusionCache targets `.NET Standard 2.0`... `.NET 5/6/7/8+`".
  Both assets are consumable from `net10.0`, so the repo's .NET 10 target is fine.
- **Microsoft relationship.** Not a Microsoft product, but: Microsoft itself uses
  FusionCache in Data API Builder (README, citing
  https://devblogs.microsoft.com/azure-sql/data-api-builder-ga/); FusionCache received a
  Google Open Source Peer Bonus award in 2021 (README, citing
  https://opensource.googleblog.com/2021/09/announcing-latest-open-source-peer-bonus-winners.html);
  and Microsoft Learn's HybridCache article explicitly names FusionCache as the example of
  a custom `HybridCache` implementation ("developers are welcome to provide or consume
  custom implementations of the API, for example FusionCache" —
  https://learn.microsoft.com/en-us/aspnet/core/performance/caching/hybrid). The
  HybridCache lead (Marc Gravell) engaged with the author in the design phase
  (MicrosoftHybridCache.md, citing dotnet/aspnetcore#53255).
- **Package family.** Core `ZiggyCreatures.FusionCache`; serializers
  (`Serialization.SystemTextJson`, NewtonsoftJson, MessagePack, ProtoBuf, MemoryPack,
  ServiceStack); backplanes (`Backplane.StackExchangeRedis`, `Backplane.Memory` for
  testing); `OpenTelemetry` and `Chaos` (controlled-failure testing) packages
  (README "Packages" section).

## 3. Core feature set, mapped to an ingestion+fetching workload

Each entry: the problem it solves, cited to the owning docs page.

- **Cache stampede protection (request coalescing)** —
  https://github.com/ZiggyCreatures/FusionCache/blob/main/docs/CacheStampede.md
  Multiple concurrent `GetOrSet` calls for the same missing key execute **one** factory;
  all callers share the result ("if 10 (or 100, or more) concurrent requests for the same
  cache key arrive... only one factory will be executed"). Local via a memory locker by
  default; an optional distributed locker extends it across nodes — which the docs
  themselves downplay: going from 100,000 concurrent DB queries to ~10 (one per node) is
  "bonkers, day/night"; going from 10 to 1 is "less so... if it's not critical I would say
  don't bother using a distributed locker."
- **Fail-safe** —
  https://github.com/ZiggyCreatures/FusionCache/blob/main/docs/FailSafe.md
  When a refresh factory throws (DB down/overloaded), temporarily re-serve the logically
  expired value instead of surfacing the error, bounded by `FailSafeMaxDuration` and
  throttled by `FailSafeThrottleDuration`. Must be enabled when the entry is saved.
- **Soft/hard timeouts** —
  https://github.com/ZiggyCreatures/FusionCache/blob/main/docs/Timeouts.md
  `FactorySoftTimeout`: if a refresh exceeds the budget, return the stale value now and let
  the factory finish in the background (background completion is on by default via
  `AllowTimedOutFactoryBackgroundCompletion`). `FactoryHardTimeout`: give up entirely with
  a `SyntheticTimeoutException`. Separate soft/hard timeouts exist for distributed-cache
  operations, plus fire-and-forget background distributed writes.
- **Eager refresh** —
  https://github.com/ZiggyCreatures/FusionCache/blob/main/docs/EagerRefresh.md
  `EagerRefreshThreshold` (a fraction of `Duration`, e.g. `0.9`): the first request
  arriving after that point — but before expiry — returns the cached value immediately and
  starts a non-blocking background refresh. Request-driven, not a timer ("we only want to
  act on data that is actively used"), and stampede protection still applies to the
  background refresh.
- **Adaptive caching** —
  https://github.com/ZiggyCreatures/FusionCache/blob/main/docs/AdaptiveCaching.md
  Factory overloads receive a `FusionCacheFactoryExecutionContext` so entry options
  (`Duration`, tags, even `SkipMemoryCacheWrite`/`SkipDistributedCacheWrite`) can be set
  based on the value just produced — e.g. cache a null briefly, a fresh entity shortly, an
  old entity longer, or skip caching a value entirely.
- **Tagging** —
  https://github.com/ZiggyCreatures/FusionCache/blob/main/docs/Tagging.md
  Entries get string tags; `RemoveByTag("tag")` logically evicts all of them at once.
  Implementation is client-assisted and passive: `RemoveByTag` writes one timestamp entry
  per tag, and reads treat entries older than the tag's timestamp as misses — no mass
  deletion, works over the 3-method `IDistributedCache` surface, and entries without tags
  pay zero extra cost. This is the feature that makes domain-scoped invalidation
  ("everything for vacancy V") practical.
- **L1+L2 hybrid mode** —
  https://github.com/ZiggyCreatures/FusionCache/blob/main/docs/CacheLevels.md
  L1 is any `IMemoryCache`; L2 is any `IDistributedCache` plus an `IFusionCacheSerializer`
  (L2 speaks `byte[]`). L2 exists to ease **cold starts** and **share data across nodes**.
  Level-specific options exist (`MemoryCacheDuration` vs `DistributedCacheDuration`,
  skip-read/write per level). The L2 payload is an envelope (value + timestamp + tags),
  and L2 keys carry a wire-format version prefix (`v2:`) so upgrades don't corrupt shared
  caches. The docs page lists available `IDistributedCache` packages including Redis,
  SQL Server, Cosmos, MongoDB, SQLite, and two PostgreSQL options (see §5).
- **Backplane** —
  https://github.com/ZiggyCreatures/FusionCache/blob/main/docs/Backplane.md
  A "slim message bus" broadcasting change notifications (key + action, no payload) so
  other nodes evict/refresh their L1 copies — without it, multi-node L1s are incoherent
  until expiry. Only two official implementations exist: Redis
  (`ZiggyCreatures.FusionCache.Backplane.StackExchangeRedis`) and an in-memory one "mainly
  for testing." The docs are explicit that a backplane without an L2 is fragile
  (notifications on every factory run cause eviction ping-pong) and that without a
  backplane the mitigation is a low `MemoryCacheDuration`.
- **DI + named caches** —
  https://github.com/ZiggyCreatures/FusionCache/blob/main/README.md and
  MicrosoftHybridCache.md. `services.AddFusionCache()` registers the default cache; the
  fluent builder chains `.WithSerializer(...)`, `.WithDistributedCache(...)`,
  `.WithBackplane(...)` (examples in CacheLevels.md/Backplane.md). Named caches with
  per-cache configuration exist (README feature list; MicrosoftHybridCache.md shows
  `.AsKeyedHybridCache("Foo")` for keyed access).

## 4. Microsoft.Extensions.Caching.Hybrid (HybridCache)

URL: https://learn.microsoft.com/en-us/aspnet/core/performance/caching/hybrid
URL: https://devblogs.microsoft.com/dotnet/hybrid-cache-is-now-ga/

- **What shipped when.** Microsoft introduced the `HybridCache` **abstraction** (abstract
  class) with **.NET 9** (November 2024); the GA announcement post is dated
  **2025-03-12** and points at package `Microsoft.Extensions.Caching.Hybrid` **9.3.0**
  (https://devblogs.microsoft.com/dotnet/hybrid-cache-is-now-ga/, citing
  https://www.nuget.org/packages/Microsoft.Extensions.Caching.Hybrid/9.3.0). The library
  targets down to .NET Framework 4.7.2 / .NET Standard 2.0 (Microsoft Learn,
  "Compatibility"), so it is not runtime-gated on .NET 9+.
- **What it does.** One-line cache-aside via `GetOrCreateAsync(key, factory)`; per-key
  stampede protection ("only one concurrent caller for a given key calls the factory
  method"); tag-based invalidation via `RemoveByTagAsync`, implemented **logically** (a
  timestamp barrier, values physically expire naturally — the same design FusionCache
  uses); L1 in-process by default with an optional `IDistributedCache` L2 (Redis,
  SQL Server, or Postgres per the docs); configurable serialization (STJ by default).
- **What it deliberately lacks (as of the docs fetched 2026-09-20).** Stampede protection
  "doesn't extend to other `HybridCache` instances" — single-node only (Microsoft Learn;
  confirmed in the devblogs comments where a Microsoft engineer replies that distributed
  locking "is not available in this initial implementation"). Tag invalidation does not
  propagate to other servers' in-memory caches (Microsoft Learn, "Cache storage" note).
  FusionCache's MicrosoftHybridCache.md adds: no L2 opt-out (any registered
  `IDistributedCache` is forced into use), no multiple named caches, no keyed services.
- **FusionCache integration.** FusionCache is a full implementation of the abstraction:
  `services.AddFusionCache().AsHybridCache()` exposes the same instance as both
  `IFusionCache` and `HybridCache`, and Microsoft's stampede protection composes with
  FusionCache's — a `HybridCache.GetOrCreateAsync("foo", ...)` and an
  `IFusionCache.GetOrSetAsync("foo", ...)` coalesce into one factory call
  (MicrosoftHybridCache.md). FusionCache was "the first production-ready implementation of
  HybridCache" including Microsoft's own (author's claim, MicrosoftHybridCache.md).
  Through the adapter, FusionCache supplies what the default implementation lacks:
  multi-node invalidation via its backplane, fail-safe, timeouts, named/keyed caches.
- **What Microsoft officially recommends.** For ASP.NET Core caching generally the docs
  present HybridCache as the modern default API; for custom implementations they explicitly
  bless third-party ones "for example FusionCache"
  (https://learn.microsoft.com/en-us/aspnet/core/performance/caching/hybrid,
  "Custom HybridCache implementations").

## 5. L2/backplane options given this repo's on-prem, likely single-instance deployment

URL: https://github.com/ZiggyCreatures/FusionCache/blob/main/docs/CacheLevels.md
URL: https://github.com/ZiggyCreatures/FusionCache/blob/main/docs/Backplane.md
URL: https://learn.microsoft.com/en-us/aspnet/core/performance/caching/distributed
URL: https://www.nuget.org/packages/Microsoft.Extensions.Caching.Postgres

- **Is Redis the only realistic L2? No — a credible first-party PostgreSQL
  `IDistributedCache` exists.** `Microsoft.Extensions.Caching.Postgres` (current 1.2.2,
  MIT, owned by the `Microsoft`/`azure-sdk` NuGet accounts, repo
  https://github.com/Azure/Microsoft.Extensions.Caching.Postgres, ~288K downloads, last
  updated ~2026-03). Microsoft Learn's distributed-caching article documents it as a
  framework-provided option (`AddDistributedPostgresCache`, with `CreateIfNotExists`,
  schema/table options, expired-items cleanup interval). FusionCache's CacheLevels.md
  package table lists it too, plus a community alternative
  (`Community.Microsoft.Extensions.Caching.PostgreSql`). Caveats: 1.2.2 is the newest
  stable and it is modest-traffic compared to the Redis package; Microsoft Learn's HybridCache
  article describes the `IBufferDistributedCache` optimization as implemented by "the
  preview versions" of the Redis/SqlServer/Postgres packages, so the stable line lags the
  newest optimizations. Also note Microsoft's general warning (stated for SQL Server,
  applicable by analogy): sharing one database for cache and app data "can negatively
  impact the performance of both."
- **Does FusionCache require a backplane? Only for multi-node L2 coherence.** Single
  instance: no backplane, no L2 needed at all. Multi-node with L2 but no backplane: L1s go
  stale until expiry, mitigated only by a low `MemoryCacheDuration` (Backplane.md). And the
  only production-grade backplane implementation is Redis — there is no PostgreSQL
  backplane package (Backplane.md lists exactly two: Redis and in-memory-for-testing). So
  multi-node coherence without standing up Redis is not realistically on the table.
- **Memory-only L1 in a single instance.** Fully supported — FusionCache "will act as a
  normal memory cache" with all resiliency features intact (CacheLevels.md); Microsoft's
  HybridCache likewise "still provides in-process caching and stampede protection" without
  any `IDistributedCache` (Microsoft Learn, "Cache storage"). The only things lost are
  cold-start survival across restarts and multi-node sharing — both irrelevant for the
  single-machine desktop and single-server on-prem topologies in
  [docs/research/2026-09-15-windows-desktop-onprem-deployment-findings.md](2026-09-15-windows-desktop-onprem-deployment-findings.md).
- **Side note for the desktop profile.** FusionCache has a documented "disk cache" concept
  (an `IDistributedCache` over local files, CacheLevels.md "Disk Cache") for self-contained
  apps that want restart-survival without a server — potentially interesting for the
  SQLite desktop profile, but not investigated further here.

## 6. Compatibility with this repo

- **.NET 10 / EF Core 10 era: compatible.** FusionCache 2.8.0's `net8.0` and
  `netstandard2.0` assets load fine on `net10.0`; it has no EF Core coupling at all (it
  sits beside the data layer, wrapping factory calls). Its dependency footprint is the
  `Microsoft.Extensions.*` abstractions train — which this repo already aligns at 10.0.12
  per ADR-0012; expect `Directory.Packages.props` to need pins for FusionCache's
  transitive `Microsoft.Extensions.Caching.Memory`/`Logging.Abstractions` etc. because
  `CentralPackageTransitivePinningEnabled` is on
  ([Directory.Packages.props](../../Directory.Packages.props)).
- **ADR-0012 posture.** "Latest stable only, no prereleases": FusionCache 2.8.0 qualifies.
  `Microsoft.Extensions.Caching.Postgres` 1.2.2 is stable but would be a new
  non-train `Microsoft.*` package outside the aligned servicing train — allowed by the
  letter of the policy (the train rule covers AspNetCore/EFCore/Extensions *servicing*
  alignment; this package ships on the Azure SDK cadence), but it deserves explicit
  scrutiny in a handoff note if ever adopted.
- **Architecture fit.** Handlers are already thin, single-purpose units behind DI, so
  wrapping a read in `GetOrSetAsync` is a per-slice change with no cross-cutting
  refactors. The ADR-0016 read seam (`ICandidateListReader`) is a natural decoration
  point: a caching decorator over the reader keeps the contract suite
  (`tests/hr-sat.Tests/Candidates/CandidateListReadContractTests.cs`) running against the
  real adapters untouched.

## 7. Impact analysis: concrete read paths

Volumes first, because they discipline everything below. This app serves one HR person (or
a handful) reviewing emailed/form applications around a vacancy: realistically tens to a
few hundred candidates per intake round, a handful of vacancies, page size 100, the
database on the same host or LAN. At this scale, PostgreSQL answers every read path below
in single-digit milliseconds; caching is a latency optimization for a problem the app does
not measurably have. The honest frame is therefore: **which paths would benefit from
caching semantics (coalescing, fail-safe) rather than raw speed, and which would caching
actively harm?**

| Read path (handler) | Cacheable? | Key shape | Invalidation trigger | Staleness tolerable? |
|---|---|---|---|---|
| `ListVacanciesQueryHandler` (dashboard) | Yes, cheap win | `vacancies:list` (plus tag `vacancy:{id}` per row if needed) | Vacancy create/update/close/reopen/purge; round opened/closed (progress counts) | Yes — seconds of staleness invisible to HR |
| `GetVacancyQueryHandler` | Yes | `vacancy:{id}` | Same vacancy mutations | Yes |
| `ListCandidatesQueryHandler` (ADR-0016 seam) | **Mostly no for active rounds** (see below); yes for **closed rounds** | `candidates:{roundId}:{filterHash}:{page}` tagged `round:{roundId}` | Import completed, candidate removed/promoted, review status change, screening rules changed | Closed rounds: data is settled — staleness impossible by definition, ideal cache target. Active rounds: **no** — ADR-0014 promises live re-evaluation ("a fixed rule re-surfaces its victims for free"); a cached list breaks that promise unless every rules edit invalidates by tag |
| `GetReviewQueueQueryHandler` / `GetCandidateDetailsQueryHandler` (incl. prior-application lookup) | Marginal | `candidate:{id}:details` tagged `vacancy:{vacancyId}` | Any candidate mutation in the vacancy (prior-application notice is cross-round) | Notes/review status are HR's live working state — staleness here risks two reviewers seeing different truth; skip at MVP |
| `PreviewScreeningRulesQueryHandler` ("would screen out N of M") | No | — | — | This is the live safety-net count (ADR-0014 §6); caching it defeats its purpose |
| `GetScreeningRulesQueryHandler`, `GetFormLayoutQueryHandler` | Yes, but pointless | per vacancy | Rules/layout edits | These are single-row reads; caching saves nothing |
| `GetMessagingSummaryQueryHandler`, EmailTemplates queries | Yes | per template/round | Template upsert/delete; candidate messaging changes | Yes |
| V3: OCR'd CV text + requirement-match results (ADR-0009) | Yes — **the strongest future candidate** | `cvtext:{documentId}`, `reqmatch:{candidateId}:{requirementSetVersion}` | Extraction completion; requirement set edited | Extraction output is immutable per document; match results derive from immutable text + ordered requirements. Staleness only if requirements change — invalidate by vacancy tag. This is genuinely expensive work (per-page OCR) whose output never changes — the textbook cache case |

The one structural insight: **the domain's "settled data" semantics are a cache
designer's gift.** Everything downstream of Round Closure (closed-round candidate lists,
frozen Screening Verdicts, review data) is immutable by decree of the lifecycle
([CONTEXT.md](../../CONTEXT.md), "Settled"; ADR-0010/0014). Immutable data needs no
invalidation strategy at all — cache it forever, evict only on memory pressure. Conversely
everything in the *active* round is deliberately live, and ADR-0014's "reclassification is
free" is a **hard tension with caching**: the only honest resolutions are (a) don't cache
active-round screening-sensitive reads, or (b) cache them under a `vacancy:{id}` tag and
pay `RemoveByTag` on every rules edit, import completion, and candidate mutation — which,
at these volumes, costs more complexity than the milliseconds it saves.

## 8. Where caching does not help

- **The ingestion path itself.** Bulk .eml uploads, CSV imports, and OCR extraction are
  writes/CPU work; a read cache does nothing for them. (Stampede protection is irrelevant:
  nobody concurrently re-imports the same file through a cache key.)
- **Small datasets.** The whole candidate corpus of a vacancy fits in a few MB; the
  ADR-0016 read seam already returns a page in one raw-SQL round trip. There is no N+1 or
  repeated-expensive-query pathology in the current codebase that caching would fix.
- **Single-user latency.** One HR user on localhost/LAN does not produce the concurrency
  that makes request coalescing valuable, nor the traffic that makes fail-safe's
  stale-serving meaningful (if Postgres is down, the app is down — fail-safe on the
  vacancy list doesn't save the review workflow).
- **Justification threshold.** Per the sizing above, caching is an MVP-scale
  *over-engineering*: the complexity (invalidation triggers, tag discipline, test
  doubles) buys single-digit milliseconds. The calculus changes only if (a) V3 OCR +
  requirement matching lands and match results are recomputed per page view, (b) the
  dashboard aggregates over many vacancies/rounds at once, or (c) a multi-user on-prem
  deployment with real concurrency appears.

## 9. Risks and costs

- **Stale review data.** The worst failure mode for this app is HR acting on a stale
  screening disposition or review status. FusionCache's tagging makes correct invalidation
  *possible*, but every write path must remember to publish the right tag — a discipline
  cost paid forever, against a domain whose central promises (ADR-0014) are about liveness.
- **Invalidation complexity vs. settled semantics.** The split in §7 (cache settled data
  freely, never cache live data) is easy to state and easy to get subtly wrong; the
  boundary moves when a round closes mid-session.
- **Dependency cost under ADR-0012.** One new top-level package (plus transitive pins under
  central package management) that must ride every catch-up sweep. FusionCache's
  maintenance record (66 releases, hours-old commits, Microsoft usage) makes this a
  low-risk dependency as dependencies go — but it is still a dependency solving an
  unmeasured problem.
- **Ops burden of any L2.** Redis on-prem contradicts the zero-config/single-container
  deployment story; `Microsoft.Extensions.Caching.Postgres` would share the app's database
  (against Microsoft's own dedicated-instance advice) for no single-instance benefit. Both
  are avoidable by staying memory-only.
- **Test impact.** The repo's flow-first testing (docs/adr/0003, docs/agents/testing.md)
  stubs at HTTP and seam boundaries; a cache inside handlers is invisible to the SQLite
  `TestDbContext` handler tests only if it's keyed and invalidated correctly — otherwise
  tests start flaking on cached state bleeding between cases. A cache-as-decorator on the
  ADR-0016 seam keeps the contract suite honest without new test infrastructure; caching
  *inside* handlers would need cache-reset fixtures.

## 10. Recommendation

**Do not adopt now. Adopt later, trigger-based, memory-only, behind the existing seams.**

- **Verdict:** FusionCache is a mature, MIT-licensed, actively maintained library with a
  feature set that is a strict superset of Microsoft's HybridCache, and it is compatible
  with this stack today. Nothing about the *library* is a risk. The app's scale and its
  deliberate live-evaluation domain semantics are the reasons to wait.
- **Adoption triggers** (any one suffices):
  1. V3 requirement matching (ADR-0009) ships and match highlighting recomputes OCR'd text
     matches per review-workspace render → cache `reqmatch`/`cvtext` keyed by document ID,
     invalidated by vacancy tag on requirement edits. This is the first slice where caching
     pays for itself, because the factory is genuinely expensive and the output is
     effectively immutable.
  2. Dashboard/list queries measurably slow with real data (pg_stat_statements evidence,
     not vibes) → cache the *closed-round* candidate list pages and vacancy list only.
  3. Multi-user or multi-node on-prem deployment appears → re-evaluate L2; note that the
     only credible backplane is Redis, so multi-node coherence means adopting Redis, full
     stop.
- **What minimal adoption looks like** (when a trigger fires): one
  `ZiggyCreatures.FusionCache` pin in `Directory.Packages.props`,
  `services.AddFusionCache().AsHybridCache()` in the API host, and handler code depending
  on the **`HybridCache` abstraction** rather than `IFusionCache`. Illustrative, not from
  the repo:

  ```csharp
  // Program.cs (illustrative)
  builder.Services.AddFusionCache().AsHybridCache();

  // In a query handler (illustrative)
  var page = await cache.GetOrCreateAsync(
      $"candidates:{roundId}:closed:{page}",
      async ct => await reader.ReadAsync(request, ct),
      tags: [$"round:{roundId}"],
      cancellationToken: ct);
  ```

  Rationale for the abstraction-first posture: `HybridCache` is Microsoft's stable
  contract, keeps the door open to swapping implementations, and — per
  MicrosoftHybridCache.md — still gets FusionCache's fail-safe/timeouts/tags underneath,
  with a future L2 becoming a one-line `.WithDistributedCache(...)` builder change rather
  than a code change. Memory-only L1 needs no Redis, no Postgres cache table, no
  backplane, and no serializer — matching the deployment reality in
  [docs/research/2026-09-15-windows-desktop-onprem-deployment-findings.md](2026-09-15-windows-desktop-onprem-deployment-findings.md).
- **Hard rules regardless of timing:** never cache active-round screening-sensitive reads
  (ADR-0014 liveness promise); never cache `PreviewScreeningRulesQueryHandler`; settled
  (closed-round) data may be cached without TTL-based anxiety; if active-round reads are
  ever cached, vacancy-scoped tags + `RemoveByTag` on every mutation are mandatory, and
  that cost should be re-weighed against just letting Postgres answer.
