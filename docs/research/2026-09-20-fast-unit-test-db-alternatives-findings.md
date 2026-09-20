# Research findings: Fast alternatives to SQLite in-memory for handler unit tests

**Date:** 2026-09-20
**Scope:** Whether the handler unit tests (currently an in-memory SQLite `TestDbContext`) have a
comparably fast, conventional, PostgreSQL-aligned alternative. Sources are primary only:
Microsoft Learn EF Core/ASP.NET Core docs, Testcontainers for .NET docs, the Respawn README, and
postgresql.org docs. All URLs fetched successfully on 2026-09-20. No fetch failures.

---

## 1. Repo context (what exists today)

Grounded in repo files, not external sources:

- `tests/hr-sat.Tests/TestDbContext.cs` — the handler-unit-test harness: `Microsoft.Data.Sqlite`
  `Data Source=:memory:`, one open connection per context, `EnsureCreated()` schema. It carries
  `DateTimeOffset → DateTime` value converters on `Vacancy.ClosedAt/CreatedAt`,
  `IntakeRound.ClosedAt`, `Candidate.SourceSentAt/ImportedAt`, and
  `CandidateFormResponse.FormTimestampParsed/ImportedAt`, plus JSON string (de)serialization
  converters for `ScreeningVerdict` and `CandidateFormResponse.Cells` — SQLite has no native
  `timestamptz` ordering or `jsonb`, so these converters are the drift tax paid up front.
- `tests/hr-sat.Tests/ApiFactory.cs` — HTTP-seam Flow Tests: `WebApplicationFactory<Program>`
  over a Testcontainers `PostgreSqlBuilder("postgres:18-alpine")` container, one container per
  factory, reset via `TRUNCATE TABLE vacancy RESTART IDENTITY CASCADE` on first client use.
- `tests/hr-sat.Tests/Candidates/CandidateListReadContractTests.cs` — abstract contract suite
  run twice per ADR-0016: the EF/LINQ `ICandidateListReader` adapter on the SQLite
  `TestDbContext`, and `PostgresCandidateListReader` (raw SQL) on Testcontainers PostgreSQL.
  Both seed through EF so fixtures are identical by construction.
- `docs/adr/0002-mvp-stack-and-api-seam.md` — the test seam decision: xUnit +
  `WebApplicationFactory` + Testcontainers Postgres for integration; Docker required for them.
- `docs/agents/testing.md` — Flow-first scope; handler unit tests over the in-memory
  `TestDbContext` "extend the seam; they do not replace it."
- Versions: .NET SDK 10.0.100 (`global.json`); EF Core 10.0.12, Npgsql EF provider 10.0.3,
  Testcontainers.PostgreSql 4.15.0, xUnit 2.9.3 (`Directory.Packages.props`).

---

## 2. Microsoft's official EF Core testing guidance

URL: https://learn.microsoft.com/en-us/ef/core/testing/
URL: https://learn.microsoft.com/en-us/ef/core/testing/choosing-a-testing-strategy
URL: https://learn.microsoft.com/en-us/ef/core/testing/testing-with-the-database

- **Testing against the production database system is Microsoft's first-choice
  recommendation.** The overview says: "Developers frequently avoid testing against their
  production database system because they believe this is difficult or slow. This isn't always
  true in our experience, and we suggest giving this approach a chance." The summary of the
  strategy page: "We recommend that developers have good test coverage of their application
  running against their actual production database system... with proper design, tests can
  execute reliably and quickly."
- **The speed objection is explicitly rebutted.** "Testing against a local database - with a
  reasonable test dataset - is usually extremely fast: communication is completely local, and
  test data is typically buffered in memory on the database side. EF Core itself contains over
  30,000 tests against SQL Server alone; these complete reliably in a few minutes... Some
  developers turn to an in-memory database (a 'fake') in the belief that this is needed for
  speed - this is almost never actually the case."
- **Testcontainers is name-checked by Microsoft** as the automation for containerized databases
  in tests: "Libraries like Testcontainers can help automate the lifecycle of containerized
  databases in your tests."
- **EF Core InMemory provider is recommended against.** Quoting the strategy page: using it as
  a database fake "is **highly discouraged**"; its summary adds "Avoid the in-memory provider
  for testing purposes - this is discouraged and only supported for legacy applications."
  Documented extra limitations vs SQLite: fewer query types (it isn't a relational database),
  transactions not supported, raw SQL completely unsupported, and "generally work[s] slower
  than SQLite in in-memory mode (or even your production database system)".
- **SQLite in-memory is the least-bad test double, with stated limits.** It "offers better
  compatibility with production relational databases, since SQLite is itself a full-fledged
  relational database. However, there will still be some important discrepancies... and some
  features cannot be tested at all (e.g. provider-specific methods on EF.Functions)." Listed
  discrepancies: case-sensitivity of string comparisons differs per provider; some queries
  aren't supported at all; provider-specific methods fail; raw SQL may fail or return different
  results. The fallback guidance: "If you've decided to use a test double, we recommend
  implementing the repository pattern... If the repository pattern isn't a viable option for
  some reason, consider using SQLite in-memory databases."
- **Real-DB isolation techniques are documented with samples:**
  - Read-only tests run in parallel against one shared database with no isolation concerns.
  - Write tests: wrap in a transaction that is never committed (implicitly rolled back on
    dispose); optionally start the transaction in the fixture's `CreateContext`.
  - Tests that manage their own transactions cannot use rollback isolation; use a separate
    database + `Cleanup()` after each test, in an xUnit collection fixture (no parallelization).
  - Parallelization by **multiple databases**: "If you have multiple test classes with tests
    which modify the database, you can still run them in parallel by having different fixtures,
    each referencing its own database. Creating and using many test databases isn't
    problematic."
  - xUnit class fixture with a lock + static flag to create/seed the database exactly once
    (collection fixtures are noted to block parallelization).
- **Respawn is name-checked by Microsoft for cleanup:** "You may also want to consider using
  the respawn package, which efficiently clears out a database... your cleanup code does not
  need to be updated as tables are added to your model." (testing-with-the-database,
  "Efficient database cleanup".)
- `EnsureDeleted()`/`EnsureCreated()` per run "can be a bit slow"; temporarily commenting out
  `EnsureDeleted` during iteration is suggested, with the caveat that the schema goes stale.

## 3. EF Core InMemory provider docs

URL: https://learn.microsoft.com/en-us/ef/core/providers/in-memory/

- "While some users use the in-memory database for testing, this is discouraged."
- Warning: "The EF Core in-memory database is not designed for performance or robustness and
  should not be used outside of testing environments."
- "Important: New features are not being added to the in-memory database."
- Described as an "in-process naive, non-performant, and non-persisted in-memory database."

## 4. SQLite-specific documented behavior

URL: https://learn.microsoft.com/en-us/ef/core/providers/sqlite/limitations
URL: https://learn.microsoft.com/en-us/dotnet/standard/data/sqlite/in-memory-databases

- **DateTimeOffset (and `decimal`, `TimeSpan`, `ulong`) are not natively supported** for
  anything beyond read/write and equality: "Other operations, however, like comparison and
  ordering will require evaluation on the client." Microsoft's recommendation: "Instead of
  `DateTimeOffset`, we recommend using `DateTime` values" with UTC conversion. This is exactly
  the gap the repo's value converters paper over — and any *ordering* on a converted column is
  client-evaluated on SQLite vs server-side `timestamptz` ordering on PostgreSQL, a genuine
  semantics drift for sorted queries.
- Modeling limitations: no schemas, no sequences, no database-generated concurrency tokens.
- In-memory lifetime semantics: `Data Source=:memory:` creates a database deleted "when the
  connection is closed... each connection creates its own database" — hence the harness's
  held-open `SqliteConnection`. Shareable variant: `Mode=Memory;Cache=Shared` persists "as long
  as at least one connection to it remains open."
- The ASP.NET Core integration-testing doc repeats: "The EF-Core in-memory database provider
  can be used for limited and basic testing, however the **SQLite provider is the recommended
  choice for in-memory testing**."
  (URL: https://learn.microsoft.com/en-us/aspnet/core/test/integration-tests)

## 5. Testcontainers for .NET — reuse and lifecycle

URL: https://dotnet.testcontainers.org/modules/postgres/
URL: https://dotnet.testcontainers.org/api/resource_reuse/
URL: https://dotnet.testcontainers.org/api/resource_reaper/

- `PostgreSqlBuilder("postgres:15.1").Build()` + `StartAsync()` is the documented module usage;
  the docs' own example shares one container as an xUnit **class fixture** "to reduce overhead."
- **Reuse is documented but experimental:** `.WithReuse(true)` retains resources across test
  runs; Testcontainers "assigns a hash value according to the builder configuration. If it
  identifies a matching resource, it will reuse this resource instead of creating a new one."
  Enabling reuse **disables the Resource Reaper** — "the resource will not be cleaned up."
- Hash collisions are possible for unconsidered builder APIs; the docs suggest a distinct label
  (`.WithLabel("reuse-id", ...)`) or a custom hash function, and warn a custom hash transfers
  compatibility responsibility to the caller.
- Explicit guidance against misuse: "**Reuse does not replace singleton implementations to
  improve test performance. Prefer proper shared instances according to your chosen test
  framework.**" — i.e., the vendor-sanctioned fast pattern is a *shared container per test run*
  (fixture), not cross-run reuse.
- Resource Reaper (Moby Ryuk): cleans up remaining Docker resources after the test run,
  successful or not. "Whenever possible, do not disable the Resource Reaper... consider
  disabling [it] only for environments that have a mechanism to cleanup Docker resources, e.g.
  ephemeral CI nodes." Only Linux containers supported.

## 6. Respawn

URL: https://github.com/jbogard/Respawn (README, latest release v7.0.0)

- Purpose: "a small utility to help in resetting test databases to a clean state. Instead of
  deleting data at the end of a test or rolling back a transaction, Respawn resets the database
  back to a clean, empty state by intelligently deleting data from tables."
- Mechanism: examines SQL metadata to "build a deterministic order of tables to delete based on
  foreign key relationships"; the order is computed once in `CreateAsync` and cached on the
  `Respawner` object. Tables/schemas can be excluded via `RespawnerOptions`.
- **PostgreSQL is a documented target:** `DbAdapter.Postgres` (adapter inferred from the
  `DbConnection` for SQL Server, PostgreSQL, MySQL, Oracle, Informix); README example resets via
  an open `NpgsqlConnection`.
- Speed claim (author's): "In benchmarks, a deterministic deletion of tables is faster than
  truncation, since truncation requires disabling or deleting foreign key constraints." Treat
  as the vendor's claim, not an independent measurement; note that this repo's current
  `TRUNCATE ... RESTART IDENTITY CASCADE` avoids per-FK work too, so Respawn's edge over the
  *status quo* reset is mainly schema-agnosticism (no table list to maintain).
- Conventionality: ~3.0k GitHub stars, 6.1k dependent repos, and — decisively — recommended
  inside Microsoft's own EF Core testing docs (section 2 above).

## 7. ASP.NET Core integration testing (sanctioned HTTP-seam pattern)

URL: https://learn.microsoft.com/en-us/aspnet/core/test/integration-tests

- `WebApplicationFactory<TEntryPoint>` + `TestServer` from `Microsoft.AspNetCore.Mvc.Testing`
  is the sanctioned pattern; this repo's `ApiFactory` already follows it.
- The doc's *customization* example swaps the DbContext to SQLite in-memory in
  `ConfigureServices` (with the "Create open SqliteConnection so EF won't automatically close
  it" comment) — i.e., Microsoft presents in-memory substitution inside the factory as a
  legitimate pattern, while also stating integration tests "use the actual components that the
  app uses in production" and advising to "limit the use of integration tests to the most
  important infrastructure scenarios."
- No relevance change to the unit-test question beyond confirming both patterns are documented;
  the repo's Flow Tests already sit on real PostgreSQL.

## 8. PostgreSQL-native speed levers (postgresql.org, PG 18 docs)

URL: https://www.postgresql.org/docs/current/sql-createdatabase.html
URL: https://www.postgresql.org/docs/current/non-durability.html

- **Template databases:** `CREATE DATABASE name TEMPLATE template_db` clones a database; the
  default `WAL_LOG` strategy "is the most efficient strategy in cases where the template
  database is small, and therefore it is the default." Constraints that matter for a
  database-per-test harness: `CREATE DATABASE` "cannot be executed inside a transaction block",
  and "no other sessions can be connected to the template database while it is being copied" —
  `CREATE DATABASE` fails if any connection exists. So: migrate once into a template database,
  then `CREATE DATABASE ... TEMPLATE` per test class for parallel isolation. This is the
  mechanism behind the "many test databases" pattern Microsoft recommends (section 2).
- **Non-durable settings** for a disposable cluster: place the data directory on a RAM disk;
  turn off `fsync`; turn off `synchronous_commit` ("risks transaction loss, though not data
  corruption, in case of a crash"); turn off `full_page_writes`; increase `max_wal_size` /
  `checkpoint_timeout`; use **unlogged tables** to skip WAL writes. All are documented as
  legitimate when durability is not required — a description that fits a throwaway test
  container exactly. None of these change query *semantics*, only durability/performance.

---

## 9. Comparison

| Option | Per-test speed | Fidelity to PostgreSQL 18 | Setup complexity | Conventional? |
|---|---|---|---|---|
| EF Core InMemory | Fastest in-process | Lowest: not relational, no transactions, no raw SQL, fewer query types | Trivial | **No** — "highly discouraged", legacy-only, no new features (§2, §3) |
| SQLite in-memory (current) | Fast in-process | Partial: relational, but no jsonb, no DateTimeOffset ordering (client-eval), case-sensitivity and SQL dialect differ, provider-specific methods untestable | Trivial (already built; converters already paid) | Yes as a *test double* — Microsoft's last-resort recommendation when the repository pattern isn't viable (§2, §4) |
| Shared PG container + transaction rollback | Fast after one container start (startup amortized once per run; Microsoft: real-DB tests "usually extremely fast") | Full: same engine, types, jsonb, SQL | Medium: shared fixture + `BeginTransaction` per test; breaks for tests that manage their own transactions (§2) | Yes — Microsoft's primary recommendation (§2) |
| Shared PG container + Respawn/TRUNCATE reset | Same container amortization; reset cost per test (Respawn: FK-ordered DELETEs, computed once; TRUNCATE CASCADE already in use) | Full | Medium: reset call in fixture; Respawn needs no table-list maintenance | Yes — Respawn is recommended by Microsoft's EF docs (§2, §6); TRUNCATE is what `ApiFactory` already does |
| Database-per-test-class via `CREATE DATABASE ... TEMPLATE` | Clone per class (fast for small templates, default `WAL_LOG` strategy) + no cross-test interference | Full | Higher: template setup, cannot clone inside a transaction, template must have no live connections | Yes — documented PG mechanism (§8) implementing Microsoft's "many test databases" pattern (§2) |
| `WithReuse(true)` container across runs | Skips container startup on repeat runs | Full | Low, but experimental; disables the Resource Reaper; docs warn to prefer in-run shared fixtures instead | Partially — documented but explicitly experimental (§5) |
| Non-durable PG settings in the test container (fsync off etc.) | Reduces write latency inside the same container pattern | Full (durability knobs, not semantics) | Low: container env/`-c` flags | Yes — postgresql.org documents these for non-durable use cases (§8) |

Note on measurement: no primary source publishes a benchmark comparing these options head to
head; the speed rows above rest on Microsoft's qualitative statements (§2) and the mechanism
descriptions in §5–§8. Anything more precise would need a repo-local benchmark — out of scope
for a docs-based finding.

---

## 10. How ADR-0016 changes the calculus

The SQLite `TestDbContext` is not only the handler-unit-test harness — it is one of the two
load-bearing legs of the ADR-0016 contract suite: the EF/LINQ `ICandidateListReader` adapter
runs on SQLite *deliberately*, as the C#-language spec that guards the SQL
`PostgresCandidateListReader`. The ADR explicitly rules out running the EF adapter on
PostgreSQL ("there is no EF-adapter-on-Postgres run, because nothing deploys that
configuration"). Consequences:

- **Switching handler unit tests to PostgreSQL does not remove SQLite from the repo** unless
  ADR-0016 is re-decided. The realistic question is therefore not "SQLite or not" but "which
  tests still ride SQLite."
- The strongest documented reason to move a test off SQLite is provider-semantics dependence
  (ordering on converted `DateTimeOffset`, jsonb shapes, `EF.Functions`, raw SQL). Handler unit
  tests mostly exercise EF-tracked entity graphs and LINQ that stays within the common
  relational subset; the provider-semantics-heavy path (the paged candidate list) is exactly
  what ADR-0016 lifted behind the reader seam and contract suite.
- Therefore the drift risk that remains inside SQLite-backed handler tests is real but narrow,
  and it is already tripwired: the contract suite runs the same scenarios against real
  PostgreSQL, and every Flow Test crosses HTTP against Testcontainers PG 18.

---

## 11. Recommendation

**Hybrid, with SQLite retained for handler unit tests — for now — and a documented trigger to
migrate.** Microsoft's own guidance makes the current setup defensible: test against the real
database where it matters (this repo already does, via Flow Tests and the ADR-0016 Postgres
run), and when a double is used at all, SQLite in-memory is the sanctioned choice over the
discouraged InMemory provider. The DateTimeOffset converters are a known, contained tax, and
ADR-0016 makes SQLite load-bearing for the reader spec regardless.

The conventional escape hatch, if handler tests start leaning on PostgreSQL semantics (jsonb
predicates, ordering on converted timestamps, `EF.Functions`, raw SQL), is **one shared
Testcontainers PG container per test run with per-test reset** — transaction rollback where
tests don't manage their own transactions, Respawn or the existing `TRUNCATE ... CASCADE`
otherwise — optionally hardened with the documented non-durable settings (`fsync=off`,
`synchronous_commit=off`, unlogged tables) or template-database cloning for parallel
isolation. That pattern is Microsoft's primary recommendation, needs no new infrastructure
(the container already exists for Flow Tests), and buys full fidelity at near-in-memory speed
once the container start is amortized. Do **not** adopt the EF Core InMemory provider, and
treat `WithReuse(true)` as optional polish only — the vendor itself says to prefer in-run
shared fixtures.

---

## 12. Sources

Primary sources (all fetched 2026-09-20):

1. https://learn.microsoft.com/en-us/ef/core/testing/ — overview; "give this approach a chance"
   for the production database; InMemory "highly limited and we discourage its use".
2. https://learn.microsoft.com/en-us/ef/core/testing/choosing-a-testing-strategy — full
   strategy comparison, per-double limitations, comparison table, summary recommendations
   (real DB first; repository pattern if a double is required; SQLite in-memory as fallback;
   avoid InMemory and DbSet mocking).
3. https://learn.microsoft.com/en-us/ef/core/testing/testing-with-the-database — fixture
   patterns, transaction-rollback isolation, multiple databases for parallelization,
   `EnsureDeleted`/`EnsureCreated` cost note, Respawn recommendation.
4. https://learn.microsoft.com/en-us/ef/core/providers/in-memory/ — "discouraged", "not
   designed for performance or robustness", "new features are not being added".
5. https://learn.microsoft.com/en-us/ef/core/providers/sqlite/limitations — DateTimeOffset /
   decimal / TimeSpan / ulong comparison-and-ordering require client evaluation; no schemas,
   sequences, native concurrency tokens.
6. https://learn.microsoft.com/en-us/dotnet/standard/data/sqlite/in-memory-databases —
   `:memory:` lifetime semantics; `Mode=Memory;Cache=Shared` sharing.
7. https://learn.microsoft.com/en-us/aspnet/core/test/integration-tests —
   WebApplicationFactory pattern; SQLite in-memory swap example; "SQLite provider is the
   recommended choice for in-memory testing".
8. https://dotnet.testcontainers.org/modules/postgres/ — PostgreSqlBuilder usage; class-fixture
   sharing to reduce overhead.
9. https://dotnet.testcontainers.org/api/resource_reuse/ — `WithReuse(true)` semantics, hash,
   disables reaper, "experimental", "does not replace singleton implementations".
10. https://dotnet.testcontainers.org/api/resource_reaper/ — Moby Ryuk cleanup; "do not disable
    ... whenever possible".
11. https://github.com/jbogard/Respawn — purpose, FK-ordered deterministic DELETE, Postgres
    adapter, benchmark claim vs truncation, v7.0.0.
12. https://www.postgresql.org/docs/current/sql-createdatabase.html — `TEMPLATE` cloning,
    `WAL_LOG` default strategy, no-transaction-block and no-open-connections constraints.
13. https://www.postgresql.org/docs/current/non-durability.html — RAM disk, `fsync`,
    `synchronous_commit`, `full_page_writes`, checkpoint tuning, unlogged tables.
