# Windows desktop + on-prem deployment findings (2026-09-15)

Research question: how to make hr-sat (ASP.NET Core 10 Kestrel Web API + Vue 3 SPA + EF Core/Npgsql/PostgreSQL + on-disk file storage) installable and desktop-native on Windows with easy, ideally zero-config, setup — for (a) fully-local single-machine use by an HR person and (b) on-prem deployment.

**TL;DR — recommended default path:** Self-contained single-file `dotnet publish -r win-x64` of the Web.Api serving the built Vue SPA as static files, launched as a per-user console/tray app bound to a dynamic localhost port, **no .NET runtime prerequisite**, fully verified against Microsoft Learn. For the database, use **SQLite via `Microsoft.EntityFrameworkCore.Sqlite` for the fully-local single-user desktop case** (zero-config, zero-service; the codebase's only Npgsql-specific runtime code is one `FOR UPDATE` raw SQL in `AppDbContext`), and keep **PostgreSQL for on-prem multi-user** (central server, Windows Service or Docker). Ship with **Velopack** (one-click Setup.exe installing to `%LocalAppData%`, no admin, delta updates, active maintenance) rather than MSIX unless enterprise IT demands MSIX/Intune — MSIX adds a mandatory code-signing trust hurdle and its one-click web-install protocol is disabled by default on consumer devices since Dec 2023. Apply EF migrations at app startup (`MigrateAsync()`) with a file-copy backup beforehand for SQLite; migration locking (EF Core 9+) makes startup migration safe for single-instance desktop use. The long-term on-prem path (same codebase as Windows Service/Docker) stays open because provider selection is a one-liner in `Program.cs` — keep it config-driven.

Caveats: NativeAOT is **not** a realistic option for this codebase (EF Core + Npgsql rely on reflection; ASP.NET Core NativeAOT explicitly excludes MVC and several middleware — only partially supports Minimal APIs). Embedding PostgreSQL via `MysticMind.PostgresEmbed` is technically possible but the package was last updated 2023-12 (verified on nuget.org), so treat it as "spike at your own risk". All version/status statements verified against primary sources on 2026-09-15; items marked *(inference)* are reasoning from those sources, not directly sourced claims.

---

## Current codebase state (verified from repo, 2026-09-15)

Facts that constrain every deployment option:

- `Program.cs` ([src/hr-sat.Web.Api/Program.cs](../../src/hr-sat.Web.Api/Program.cs)):
  - `UseNpgsql(builder.Configuration.GetConnectionString("Database"))` is the **only** provider hook-up — a provider swap is a one-line change plus DI wiring.
  - `dbContext.Database.MigrateAsync()` and `SeedAsync` run **only inside `IsDevelopment()`**. Production startup migration is not wired.
  - **No** `UseStaticFiles` / `MapStaticAssets` / `MapFallbackToFile` — the Vue SPA is not served in production. Dev uses the Vite proxy (per `docs/discovery/04-architecture.md`).
  - `FileDeletionSweeper` BackgroundService + `PrivateFileStorage` writing to `private-files/` (relative path — must move to a per-user data dir for a packaged app).
- Npgsql-specific surface (grep of `src/hr-sat.Infrastructure`, excluding `bin/obj`):
  - One Postgres-only runtime statement: `FromSqlInterpolated($"SELECT * FROM vacancy WHERE id = {id} FOR UPDATE")` in [AppDbContext.cs](../../src/hr-sat.Infrastructure/Infrastructure/AppDbContext.cs#L45) (row locking; SQLite would need a different strategy — SQLite serializes writes anyway, so the lock is effectively a no-op requirement there, *(inference)*).
  - Migrations use `NpgsqlValueGenerationStrategy.IdentityAlwaysColumn` throughout — migrations are provider-specific, so a SQLite provider needs its own migration chain (normal EF Core practice, *(inference)*).
  - No `jsonb`, `citext`, tsvector/full-text search, or `ILike` usage found in code yet (ADR-0009 full-text matching is planned but not implemented in Infrastructure at time of writing).
- Tests: Testcontainers PostgreSQL (see `tests/hr-sat.Tests/ApiFactory.cs`) — tests assume a real Postgres server.

---

## 1. Desktop packaging / shell options

### 1.1 Self-contained single-file Kestrel exe serving the SPA

`dotnet publish` supports single-file output for both framework-dependent and self-contained apps; self-contained bundles the runtime and needs **no .NET install** on the target. Single-file is always RID-specific (e.g. `win-x64`), and only managed DLLs are bundled by default — native libraries (relevant if Sdcb.PaddleOCR ships native assets) are separate files unless `IncludeNativeLibrariesForSelfExtract=true` is set, which extracts to `%TEMP%/.net` on Windows. Optional `EnableCompressionInSingleFile=true` shrinks the exe at a startup cost. ([Microsoft Learn — single-file deployment](https://learn.microsoft.com/en-us/dotnet/core/deploying/single-file/overview))

Known API caveats: `Assembly.Location` returns empty string, `Assembly.CodeBase` throws — use `AppContext.BaseDirectory` for files next to the exe ([same page](https://learn.microsoft.com/en-us/dotnet/core/deploying/single-file/overview#api-incompatibility)). hr-sat's `PrivateFileStorage` and any "files next to exe" logic must use `AppContext.BaseDirectory` or better, `%LocalAppData%`.

For the SPA: `StaticFiles` middleware is fully compatible ([ASP.NET Core Native AOT compatibility table](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/native-aot) lists StaticFiles as supported even under AOT); hr-sat would add `MapStaticAssets()`/`UseStaticFiles()` + `MapFallbackToFile("index.html")` in `Program.cs` and copy `wwwroot` from the Vite build at publish time (standard pattern, *(inference)*). Note the `Spa` *middleware package* (`Microsoft.AspNetCore.SpaProxy` style dev-time integration) is listed as **not supported** under NativeAOT — irrelevant here since dev proxying is via Vite.

Port strategy: `UseUrls("http://127.0.0.1:0")` binds a dynamic port — Kestrel endpoint configuration via `ASPNETCORE_URLS` or config is documented in the [Windows Service hosting article](https://learn.microsoft.com/en-us/aspnet/core/host-and-deploy/windows-service?view=aspnetcore-10.0#configure-endpoints) and [Kestrel endpoints](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/servers/kestrel/endpoints). Dynamic port avoids conflicts; launch the browser (or shell) at the bound address after startup, *(inference)*.

- **What the user installs:** one exe + the SPA assets + (later) OCR native assets. Nothing else.
- **Prerequisites:** none (self-contained).
- **Offline:** fully offline.
- **Trade-offs:** no native window — user sees a browser tab pointing at localhost (or a tray icon). URL/port changes between runs if dynamic; deep links/bookmarks unstable, *(inference)*.

### 1.2 NativeAOT — ruled out for this app

ASP.NET Core NativeAOT compatibility ([Microsoft Learn](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/native-aot), current moniker aspnetcore-10.0): MVC ❌, Blazor Server ❌, Session ❌, **Minimal APIs only partial ✔️**, StaticFiles ✔️. The AOT template mandates `CreateSlimBuilder()` (no HTTPS/HTTP3, no `UseStaticWebAssets`, no IIS integration) and source-generated JSON. General NativeAOT limitations: no dynamic loading, no runtime codegen, requires trimming with its own incompatibilities ([Microsoft Learn — Native AOT deployment](https://learn.microsoft.com/en-us/dotnet/core/deploying/native-aot/#limitations-of-native-aot-deployment)). EF Core's reliance on reflection makes EF Core + Npgsql effectively incompatible with trimming/AOT today (the EF Core docs direct trimming/AOT users to compiled models and Dapper-style approaches; see known trimming incompatibilities linked from both pages above). Conclusion: skip NativeAOT; plain self-contained single-file is the right payload.

### 1.3 WebView2-based shells (Photino.NET / raw WebView2) vs Electron vs Tauri

| Shell | Runtime prereq | What ships | Offline | Notes |
|---|---|---|---|---|
| Self-contained exe + browser | none | one exe (~self-contained size) | yes | simplest; browser UI |
| [Photino.NET](https://github.com/tryphotino/photino.NET) | WebView2 evergreen runtime | thin native window over OS browser control | yes (if runtime present/bundled) | lightweight (repo claims "up to 110x smaller than Electron"); repo note 2026-03-26: project is transitioning to AI-assisted maintenance due to team time constraints — **bus-factor risk** |
| Tauri 2 | WebView2 on Windows | Rust shell + your SPA | yes | Tauri docs: "WebView2 is already installed on Windows 10 (from version 1803 onward) and later" ([tauri.app prerequisites](https://tauri.app/start/prerequisites/)) |
| Electron | none (bundles Chromium) | Chromium + Node, ~150 MB+ | yes | heaviest; no .NET integration — would host Kestrel as sidecar, *(inference)* |

WebView2 presence guarantees, from [Microsoft Edge docs — Distribute your app and the WebView2 Runtime](https://learn.microsoft.com/en-us/microsoft-edge/webview2/concepts/distribution):
- The evergreen runtime **is included with Windows 11**; "the vast majority of Windows 10 devices have the WebView2 Runtime installed already" (Microsoft pushed it to managed Win10 devices in Dec 2022), but some devices lack it — apps **should check and install** (bootstrapper ~2 MB online, or standalone offline installer; per-user install happens when run non-elevated).
- Production apps may not rely on the Edge Stable browser; the WebView2 **Runtime** is the only supported production backing.
- Fixed Version runtime (bundle your own ~250 MB copy) exists for constrained environments.
- If the app is packaged as MSIX, the WebView2 runtime can be declared as a package dependency so it installs with the app ([win32dependencies:ExternalDependency](https://learn.microsoft.com/en-us/uwp/schemas/appxpackage/uapmanifestschema/element-win32dependencies-externaldependency), referenced from the distribution page).

### 1.4 Windows Service vs per-user tray app

ASP.NET Core can be hosted as a Windows Service without IIS via `Microsoft.Extensions.Hosting.WindowsServices` (`builder.Services.AddWindowsService()` / `UseWindowsService()`); the service then starts automatically after reboots, content root becomes `AppContext.BaseDirectory`, and event-log logging is enabled (event-source creation needs admin). Service creation is `New-Service` from an **administrative** shell, with a service account needing "Log on as a service" rights. Self-contained deployment works fine for services ([Microsoft Learn — Host ASP.NET Core in a Windows Service](https://learn.microsoft.com/en-us/aspnet/core/host-and-deploy/windows-service?view=aspnetcore-10.0)).

- **Per-user tray/console app**: right for fully-local desktop use — no admin, per-user data in `%LocalAppData%`, dies at logoff (fine for a personal tool).
- **Windows Service**: right for the on-prem single-server variant — always-on, survives reboots, needs admin to install. Not for the desktop product.

### Recommendation for hr-sat (shell)

**Phase 1: self-contained single-file Kestrel exe + system browser + tray icon (optional, small `SystemTray`-style wrapper), dynamic localhost port.** No new dependency, no WebView2 assumption, works on any Win10/11 box. Add `MapStaticAssets` + fallback for the SPA (required regardless). Consider a WebView2 thin shell later only if "must feel like an app window" becomes a hard requirement — and prefer raw WebView2 or Tauri over Photino given Photino's announced maintenance-model transition ([repo README](https://github.com/tryphotino/photino.NET)).

---

## 2. The database problem

### 2.1 Bundle/embed PostgreSQL

- **Official binaries:** postgresql.org links to the EDB interactive installer and, for "advanced users who wish to include Postgres as part of another application installer", a **zip archive of the binaries** ([postgresql.org/download/windows](https://www.postgresql.org/download/windows/) → [EDB binaries zip](https://www.enterprisedb.com/download-postgresql-binaries)). So the "ship the zip, `initdb` at first run, start `postgres.exe` on a chosen port" approach uses first-party binaries. Costs: ~300 MB payload, process lifecycle management (start/stop/crash recovery), port conflicts (default 5432), antivirus interference, and upgrade story are all on you, *(inference)*.
- **.NET embedder:** [MysticMind.PostgresEmbed](https://github.com/mysticmind/mysticmind-postgresembed) wraps download/start/stop of Postgres binaries. NuGet shows **4.0.0, last updated 2023-12-14** ([nuget.org/packages/MysticMind.PostgresEmbed](https://www.nuget.org/packages/MysticMind.PostgresEmbed)) — ~2.75 years stale at research date; targets .NET 6. The author is active elsewhere (Marten), but this package itself looks unmaintained. Usable as inspiration or a spike; don't bet the product on it without owning the fork, *(inference)*.
- **In-process Postgres does not exist** for Windows/.NET (unlike SQLite/embedded H2 in JVM-land). "Embedded" always means "bundled server process", *(inference — consistent with the MysticMind design and EDB binary distribution model)*.

### 2.2 SQLite via `Microsoft.EntityFrameworkCore.Sqlite`

EF Core's documented SQLite limitations ([Microsoft Learn — SQLite provider limitations](https://learn.microsoft.com/en-us/ef/core/providers/sqlite/limitations)):
- **Modeling:** no schemas, no sequences, no database-generated concurrency tokens.
- **Query:** `DateTimeOffset`, `decimal`, `TimeSpan`, `ulong` aren't natively supported — equality works, but comparison/ordering evaluate client-side; docs recommend `DateTime` + UTC and `double` (or a value converter) instead of `decimal`.
- **Migrations:** several schema ops require a table **rebuild** (AddForeignKey, AlterColumn, DropColumn, etc. — full table in the doc); **idempotent migration scripts are not supported** on SQLite.
- **Concurrency:** SQLite serializes writers (single-writer engine) — fine for single-user desktop, wrong for multi-user on-prem, *(inference; consistent with the provider's lock-table workaround documented under "Concurrent migrations protection" on the same page)*.
- **Migration locking quirk (EF9+):** SQLite uses an `__EFMigrationsLock` table; if the process is killed mid-migration the lock row can be orphaned and block future migrations until the table/rows are dropped manually ([same page](https://learn.microsoft.com/en-us/ef/core/providers/sqlite/limitations#concurrent-migrations-protection)). A desktop app must handle this at startup (delete stale lock on boot), *(inference)*.

**Provider-swap cost for hr-sat (verified in repo):** provider hook-up is a one-liner in `Program.cs`; entity configs use only cross-provider fluent API plus `IdentityAlwaysColumn` migrations (migrations must be regenerated for SQLite — normal practice); the one `FOR UPDATE` raw SQL in `AppDbContext.LockVacancyRowAsync` needs a provider-conditional branch (on SQLite: just read the row inside the transaction, *(inference)*). Tests currently need real Postgres (Testcontainers) — a SQLite test path can run fast in-memory, but keeping Postgres for seam tests on CI is advisable so both providers are covered, *(inference)*.

### 2.3 SQL Server LocalDB (middle option)

From [Microsoft Learn — SQL Server Express LocalDB](https://learn.microsoft.com/en-us/sql/database-engine/configure-windows/sql-server-express-localdb): a minimal, per-user, on-demand SQL Server instance; zero service; DB files under `%LOCALAPPDATA%\Microsoft\Microsoft SQL Server Local DB\Instances\...`; connect via `(localdb)\MSSQLLocalDB`. **But:** it is a **separate install** (`SqlLocalDB.msi` from SQL Server Express media or the Visual Studio Installer), Windows-only, and can't be managed remotely. For "zero-config desktop" it fails the "user installs nothing extra" bar, and it swaps one provider dependency for a heavier one than SQLite. Viable only if the org standardizes on SQL Server, *(inference)*.

### 2.4 DB comparison table

| Option | Zero-config single-user | Multi-user on-prem | Install footprint | Migrations story | Maintenance risk |
|---|---|---|---|---|---|
| SQLite (EF provider) | ✅ best | ❌ single-writer | 1 DLL in publish output | startup `MigrateAsync` + file-copy backup; no idempotent scripts | none — Microsoft-owned provider |
| Bundled Postgres (EDB zip + initdb) | ⚠️ heavy but possible | ✅ same engine everywhere | ~300 MB + data dir + port + service/process mgmt | startup `MigrateAsync`; pg_dump backup | self-managed lifecycle; MysticMind stale since 2023-12 ([nuget](https://www.nuget.org/packages/MysticMind.PostgresEmbed)) |
| External Postgres server | ❌ (user must install/config) | ✅ the real answer for on-prem | none in app | startup migrate OK single-server; EF9+ lock protects concurrency | ops burden on customer IT |
| LocalDB | ⚠️ extra MSI install | ❌ per-user only | separate installer | same as SQL Server | fine, but pointless vs SQLite here, *(inference)* |

### Recommendation for hr-sat (database)

**Dual provider: SQLite as the default for the desktop edition (DB file in `%LocalAppData%\hr-sat\`), Npgsql/PostgreSQL for dev, CI seam tests, and the on-prem server edition.** Select provider from config (`"Database:Provider": "sqlite|npgsql"`) in `Program.cs`. Gate or reimplement the single `FOR UPDATE` usage per provider. Keep Testcontainers-Postgres tests as the authoritative seam suite; add a lightweight SQLite integration pass for desktop-critical flows. Defer embedded-Postgres bundling — spike only if dual-provider proves painful.

---

## 3. Installer + zero-config setup on Windows

| Tech | Admin? | Per-user install | Auto-update | Delta/bandwidth | Signing friction | Source |
|---|---|---|---|---|---|---|
| **MSIX + .appinstaller** | no (per-user) | yes (per-user is the model) | yes — background checks against an update URI (HTTP/file share), on-launch check, forced update options | yes — only changed 64 KB blocks downloaded ([MSIX overview](https://learn.microsoft.com/en-us/windows/msix/overview)) | **High** — package must be signed **and trusted** on the device; self-signed cert must be deployed to Trusted People per device ([signing docs](https://learn.microsoft.com/en-us/windows/msix/package/signing-package-overview), [web install docs](https://learn.microsoft.com/en-us/windows/msix/app-installer/installing-windows10-apps-web)) | Microsoft Learn |
| **Velopack** | no | yes — Setup.exe is one-click, installs to `%LocalAppData%\{packId}`, no elevation ([installer docs](https://docs.velopack.io/packaging/installer)) | yes — `releases.json` index + `Update.exe`; delta `.nupkg` between versions, portable zip option ([packaging docs](https://docs.velopack.io/packaging/overview)) | yes — delta packages, zstd patches (repo notes bsdiff removed, zstd only) | SmartScreen on unsigned exe; signing recommended but not enforced ([packaging docs](https://docs.velopack.io/packaging/overview)) | docs.velopack.io, [GitHub](https://github.com/velopack/velopack) |
| **WiX (MSI)** | typically yes (per-machine); per-user MSI possible | both | no built-in | no (full MSI) | SmartScreen on unsigned MSI | [docs.firegiant.com/wix](https://docs.firegiant.com/wix/using-wix/) (WiX as `dotnet tool`, v7 SDK-style; Velopack itself generates its optional MSI with WiX 5, [installer docs](https://docs.velopack.io/packaging/installer)) |
| **Inno Setup / NSIS (exe)** | typically yes; Inno supports non-admin per-user installs | both | no built-in (pair with NetSparkle) | no | SmartScreen | [jrsoftware.org](https://jrsoftware.org/isinfo.php) *(referenced by NetSparkle README as recommended pairing)* |
| **ClickOnce** | no — "no administrative rights are required for installation"; per-user cache, nothing in Program Files/registry | yes | yes — manifest update checks, mandatory updates, rollback to earlier version | only changed files re-downloaded ([ClickOnce docs](https://learn.microsoft.com/en-us/visualstudio/deployment/clickonce-security-and-deployment)) | trust prompt unless signed by trusted cert | [Microsoft Learn — ClickOnce](https://learn.microsoft.com/en-us/visualstudio/deployment/clickonce-security-and-deployment) |

**MSIX specifics:**
- Supported on Windows 10 1709+ / Server 2019+ ([supported platforms](https://learn.microsoft.com/en-us/windows/msix/supported-platforms)). Auto-update/repair via App Installer file requires Windows 10 2004+ ([auto-update docs](https://learn.microsoft.com/en-us/windows/msix/app-installer/auto-update-and-repair--overview)).
- **⚠️ One-click web install is effectively dead for consumers:** the `ms-appinstaller:` protocol handler is **disabled by default since Dec 2023**; re-enabling needs Group Policy (`EnableMSAppInstallerProtocol`). Non-enterprise distribution should link a plain `.appinstaller` file download instead ([installing from a web page](https://learn.microsoft.com/en-us/windows/msix/app-installer/installing-windows10-apps-web)).
- **Signing:** all MSIX packages must be signed; the cert must chain to a trusted root on the device. Options: self-signed (free, but cert must be pre-deployed to each device's Trusted People store — real friction for ad-hoc internal rollout), Azure Artifact Signing (~$10/month, recommended by Microsoft; org eligibility limited to US/CA/EU/UK with 3+ years verifiable tax history), OV cert ($300–500/yr), or Store signing ([signing overview](https://learn.microsoft.com/en-us/windows/msix/package/signing-package-overview)). New apps still show SmartScreen warnings until reputation builds, even signed ([same page](https://learn.microsoft.com/en-us/windows/msix/package/signing-package-overview)).
- MSIX per-user installs don't need admin; per-machine provisioning does. Windows Services inside MSIX require Win10 2004+ ([supported platforms](https://learn.microsoft.com/en-us/windows/msix/supported-platforms)).

**ClickOnce status on .NET 5+:** supported (publish from VS Publish tool; `dotnet-mage.exe` replaces Mage.exe for manifests) but it's a legacy-feeling stack — per-user cache layout, trust prompts, and no modern delta story. Velopack's docs even ship a "From ClickOnce" migration guide ([docs.velopack.io](https://docs.velopack.io/)). Not recommended for a new app, *(inference)*.

**Data placement:** per-user data (SQLite DB, `private-files/`, logs) belongs in `%LocalAppData%\hr-sat\` (Velopack already installs the app there; MSIX redirects writes into the package's per-user app-data area — either way, code should use `Environment.GetFolderPath(SpecialFolder.LocalApplicationData)`, *(inference; consistent with LocalDB docs using `%LOCALAPPDATA%` and MSIX containerization behavior in the [MSIX overview](https://learn.microsoft.com/en-us/windows/msix/overview))*).

**Velopack health:** actively maintained — latest release 1.2.0 (~3 months before research date), commits within weeks, 2.3k stars, successor lineage from Clowd.Squirrel/Squirrel.Windows ([GitHub repo](https://github.com/velopack/velopack)).

### Recommendation for hr-sat (installer)

**Velopack** for the desktop edition: one-click no-admin Setup.exe into `%LocalAppData%`, delta updates from any static host (internal file share/HTTPS), zero config. Reconsider MSIX only if corporate IT explicitly wants Intune/GPO-managed deployment — then budget for a signing solution (Azure Artifact Signing if the org is eligible; otherwise an internal CA cert pushed via GPO removes the per-device trust pain, *(inference)*). Sign whatever you ship regardless; unsigned anything triggers SmartScreen.

---

## 4. Incremental deployments / updates and the long-term path

### 4.1 Auto-update mechanics

- **MSIX/.appinstaller:** app checks its App Installer URI (file share or HTTPS; server must support byte-range requests for web install, and the `.appinstaller`/`.msix` MIME types must be configured, [web-install docs](https://learn.microsoft.com/en-us/windows/msix/app-installer/installing-windows10-apps-web)); update cadence via `HoursBetweenUpdateChecks`, on-launch checks, `UpdateBlocksActivation` (block launch until updated) ([auto-update docs](https://learn.microsoft.com/en-us/windows/msix/app-installer/auto-update-and-repair--overview)). Differential download at 64 KB block granularity ([MSIX overview](https://learn.microsoft.com/en-us/windows/msix/overview)).
- **Velopack:** `UpdateManager` reads `releases.{channel}.json` from any static host; applies delta `.nupkg` when available, full package otherwise; updates relaunch in seconds without UAC ([packaging overview](https://docs.velopack.io/packaging/overview), [repo README](https://github.com/velopack/velopack)).
- **NetSparkleUpdater:** appcast (XML/JSON, Ed25519-signed) + downloads a full installer (exe/msi) — no delta; you keep the installer toolchain (Inno/WiX) yourself ([GitHub README](https://github.com/NetSparkleUpdater/NetSparkle)). Actively maintained (3.1.0, ~4 months old) but strictly a poorer fit than Velopack for this app, *(inference)*.

### 4.2 EF Core migrations on an auto-updating desktop app

From [Microsoft Learn — Applying Migrations](https://learn.microsoft.com/en-us/ef/core/managing-schemas/migrations/applying):
- **Runtime migration** (`context.Database.MigrateAsync()` at startup) is an officially listed strategy — "acceptable for applications that prefer simple deployment and can tolerate startup migration behavior". **EF Core 9+ wraps it in a database-wide lock**, so concurrent instances can't corrupt each other. Caveats listed: app needs schema-altering permissions; SQL isn't reviewed before execution; **rollback of applied migrations is the hard part** — other strategies (SQL scripts, migration bundles) give rollback more easily.
- hr-sat's `MigrateAsync()` currently runs only in Development ([Program.cs](../../src/hr-sat.Web.Api/Program.cs#L41-L46)) — for the desktop build, move migration out of the `IsDevelopment()` guard (keep seeding dev-only), *(inference)*.
- EF9+ also **throws on `Migrate()` when the model has pending changes** vs the last migration — add `dotnet ef migrations has-pending-model-changes` to CI ([same page](https://learn.microsoft.com/en-us/ef/core/managing-schemas/migrations/applying#migration-locking)).
- **Update-rollback vs DB-forward-migration:** Microsoft's doc explicitly warns rollback (`Down`) "may result in data loss". Community/ops consensus (secondary): treat migrations as **forward-only**; if the app is rolled back, the newer-schema DB may break the older app — mitigate with expand/contract migrations (additive-only per release, destructive changes a release later), *(secondary/community practice, consistent with the data-loss warnings in the EF docs)*.
- **Backup before migrate:** SQLite backup = copy the file while the app holds no open connection (or `VACUUM INTO`), trivial and safe, *(inference)*. Postgres backup = `pg_dump` — fine on a server, awkward in a zero-config desktop bundle (another point against bundled Postgres, *(inference)*).
- SQLite stale-lock hazard from section 2.2 applies here directly: killed mid-migration → manually clear `__EFMigrationsLock` ([SQLite limitations](https://learn.microsoft.com/en-us/ef/core/providers/sqlite/limitations#concurrent-migrations-protection)).

### 4.3 Backup/restore UX for a local app

With the SQLite design, the entire app state = one DB file + the `private-files/` tree under `%LocalAppData%\hr-sat\`. Export = zip those; import = replace and restart. That is the whole UX — build a menu item for it, *(inference)*.

### 4.4 Long-term roadmap

- **Phase 1 (now):** zip + self-contained single-file exe, SQLite default, startup migration, browser-based UI. Validates the whole local story with zero installer work.
- **Phase 2:** Velopack package (Setup.exe + delta updates from an internal share). Code-sign. Add tray icon + backup/export UX.
- **Phase 3 (on-prem multi-user):** same codebase, Npgsql provider selected via config, published self-contained, run as a **Windows Service** on one Windows machine/VM ([Windows Service hosting](https://learn.microsoft.com/en-us/aspnet/core/host-and-deploy/windows-service?view=aspnetcore-10.0)) or in Docker; desktops connect via browser. This is the cheapest on-prem story — no desktop client needed at all for multi-user.
- **Decisions now that keep the path open:** provider selected by config (done — one line in Program.cs); connection string from config/env only (already true); port configurable (Kestrel `UseUrls`/env — trivial); no Postgres-only SQL in feature code (only the one `FOR UPDATE` — gate it now); file storage behind `IPrivateFileStorage` with configurable root (already true).

### Recommendation for hr-sat (updates & path)

Velopack delta updates + `MigrateAsync()` at startup + copy-the-file backup before migrating + expand/contract migration discipline + forward-only releases (never ship a "rollback" that expects a downgraded DB). Treat the Windows-Service central deployment as the answer to "on-prem multi-user" rather than stretching the desktop single-user install.

---

## Open questions / decisions needed (owner)

1. **DB provider split:** commit to dual-provider (SQLite desktop / Postgres on-prem) now, or spike bundled Postgres (EDB zip + initdb, possibly forking MysticMind.PostgresEmbed, stale since 2023-12) to keep one engine?
2. **Signing budget:** Velopack SmartScreen tolerance vs Azure Artifact Signing (~$10/mo, org eligibility constraints) vs internal CA cert via GPO. If MSIX is ever chosen, signing+trust becomes mandatory, not optional.
3. **Port strategy:** dynamic port (`http://127.0.0.1:0`, immune to conflicts, unstable bookmarks) vs fixed configurable port (e.g. 5xxx, risk of collisions). Default: dynamic.
4. **The `FOR UPDATE` lock:** how should `LockVacancyRowAsync` behave on SQLite (no-op read inside transaction vs optimistic concurrency token)? Decide when the SQLite provider lands.
5. **Update hosting:** where does `releases.json` live — internal file share, internal HTTPS server, or GitHub Releases (private repo needs auth headers)?
6. **Backup policy UX:** automatic backup-on-every-update vs on-demand export; retention count; where backups live.
7. **Multi-user target OS:** if on-prem server edition happens — Windows Service on a Windows VM, or Docker on Linux? (Both work from the same codebase; affects nothing now.)
8. **OCR payload (ADR-0009):** if Sdcb.PaddleOCR ships, native assets must be excluded from single-file bundling or handled via `IncludeNativeLibrariesForSelfExtract`; decide publish profile then.
9. **MSIX ever?** Only if IT demands Intune/GPO-managed deployment. If yes, the `ms-appinstaller:` protocol being disabled by default (Dec 2023) must be planned around via GPO or plain file download.

## Primary sources

- Single-file publish: https://learn.microsoft.com/en-us/dotnet/core/deploying/single-file/overview
- Native AOT: https://learn.microsoft.com/en-us/dotnet/core/deploying/native-aot/ and https://learn.microsoft.com/en-us/aspnet/core/fundamentals/native-aot
- Windows Service hosting: https://learn.microsoft.com/en-us/aspnet/core/host-and-deploy/windows-service?view=aspnetcore-10.0
- WebView2 distribution: https://learn.microsoft.com/en-us/microsoft-edge/webview2/concepts/distribution
- Tauri prerequisites: https://tauri.app/start/prerequisites/
- Photino.NET: https://github.com/tryphotino/photino.NET
- EF Core SQLite limitations: https://learn.microsoft.com/en-us/ef/core/providers/sqlite/limitations
- EF Core applying migrations: https://learn.microsoft.com/en-us/ef/core/managing-schemas/migrations/applying
- LocalDB: https://learn.microsoft.com/en-us/sql/database-engine/configure-windows/sql-server-express-localdb
- PostgreSQL Windows downloads: https://www.postgresql.org/download/windows/
- MysticMind.PostgresEmbed: https://www.nuget.org/packages/MysticMind.PostgresEmbed and https://github.com/mysticmind/mysticmind-postgresembed
- MSIX: https://learn.microsoft.com/en-us/windows/msix/overview , https://learn.microsoft.com/en-us/windows/msix/supported-platforms , https://learn.microsoft.com/en-us/windows/msix/app-installer/auto-update-and-repair--overview , https://learn.microsoft.com/en-us/windows/msix/app-installer/installing-windows10-apps-web , https://learn.microsoft.com/en-us/windows/msix/package/signing-package-overview
- Velopack: https://github.com/velopack/velopack , https://docs.velopack.io/ , https://docs.velopack.io/packaging/overview , https://docs.velopack.io/packaging/installer
- WiX: https://docs.firegiant.com/wix/using-wix/
- ClickOnce: https://learn.microsoft.com/en-us/visualstudio/deployment/clickonce-security-and-deployment
- NetSparkle: https://github.com/NetSparkleUpdater/NetSparkle
