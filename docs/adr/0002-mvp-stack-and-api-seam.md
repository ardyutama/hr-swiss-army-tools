# ADR 0002: MVP stack and the /api seam

## Status

Accepted (2026-08-27). Amended by ADR-0005 (2026-08-31): `dotnet ef` migrations replace
the deferred `EnsureCreated()` lifecycle.

## Context

The repo started from the Visual Studio "ASP.NET Core with Vue" template, which is built
to demo the stack: per-endpoint Vite proxying, dev-certificate machinery in
`vite.config.ts`, HTTPS redirect middleware, a WeatherForecast endpoint inlined in
`Program.cs`, welcome-kit components under a type-based `components/` folder, and no test
projects on either side. The MVP spec (`.scratch/mvp/spec.md`) instead wants a Vertical
Slice monolith, feature folders on the client, one test seam per side, and single-Docker
deployment.

## Decision

**The /api seam.** All API endpoints live under `/api/*`. Plain HTTP everywhere: the Vite
dev server proxies `/api` to `http://localhost:5086`; in production the single container
serves `dist/` and the API from the same origin. Dev-certificate generation, the `https`
launch profile, and `UseHttpsRedirection` are deleted. If the tool ever leaves the LAN,
TLS terminates at a reverse proxy in front of the container.

**Response shape.** Plain JSON on success; RFC 7807 ProblemDetails on errors. No envelope.

**Server shape.** `Program.cs` is the composition root only. Each feature slice maps its
own endpoints from `Features/<Feature>/`; a single `AppDbContext` (Npgsql) lives in
`Infrastructure/`; entities in `Domain/`. OpenAPI stays, dev-only, with Scalar as the API
reference UI.

**Database lifecycle.** PostgreSQL 18 in `compose.yml`. Schema is created with EF Core
`EnsureCreated()` at startup. Migrations are deferred deliberately: introduce `dotnet ef`
migrations the first time the schema changes while data worth keeping exists. This is a
deferral, not a rejection — do not "fix" it ahead of that trigger.

**Test seams.** Backend: xUnit + WebApplicationFactory + Testcontainers Postgres — every
integration test crosses the HTTP seam against a hermetic throwaway database container.
Frontend: Vitest + @vue/test-utils + jsdom — component tests at the feature-component seam.
Both harnesses exist before feature slices land.

**Client shape.** `src/features/<feature>/` (view + composables + api client),
`src/shared/` (thin kernel: `http.ts`, layout, primitives), vue-router from the start.
The `.esproj` stays as a Visual Studio F5 launcher; Docker (multi-stage build into
`wwwroot`) owns the shipping client build.

## Consequences

- New slices need zero client config: call `/api/...`, done.
- Deleted as part of this decision: 12 welcome-kit files, starter CSS, cert machinery,
  the weatherforecast endpoint, the `.http` demo file, template CHANGELOG/README.
- Docker must be running for backend integration tests (Testcontainers).
- `EnsureCreated` means no `__EFMigrationsHistory`; when migrations arrive, existing dev
  databases should be dropped and recreated.
- Future reviews should not re-suggest HTTPS-in-dev, per-endpoint proxying, or a JSON
  envelope without new constraints.
