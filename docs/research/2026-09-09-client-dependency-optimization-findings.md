# Research findings: client dependency optimization

**Date:** 2026-09-09
**Question:** Which dependencies/libraries could this app adopt to optimize it — the
headline candidate being TanStack Query for the Vue client — and what else is worth
adopting, deferring, or deliberately skipping?

## Scope

The emphasis is the Vue client (`src/hr-sat.Client/`, Vue 3.5 + Vite 8 + Nuxt UI 4 +
vue-router 4.6 + zod 4.5 + vue-pdf-embed 2.1). A short server-side (.NET 10) section is
included only where clearly high-value. Candidates were evaluated against the repo's
architecture rules: one composable per async lifecycle owning loading/error/data plus a
`loading|error|empty|ready` view-state union (AGENTS.md); seam tests stub `fetch` via
`vi.stubGlobal` (ADR-0003, `docs/agents/vue.md`); shared code is earned at the second
same-reason consumer; Nuxt UI v4 is the UI source of truth (ADR-0007); oxlint is the
sole linter (ADR-0001).

Relevant client facts verified in-repo before evaluating anything:

- Routes are already lazy-loaded via dynamic imports in
  `src/hr-sat.Client/src/router.ts` (all three page views).
- Data flows use hand-rolled composables: `useVacancyDetail` implements a stale-response
  guard with a request token, a manual `load()` refetch, and reload-on-route-param via
  `watch`; `useVacancyDetailFlow` manually coordinates refresh after import/delete
  (`Promise.all([load(), loadCandidates()])`, `await load()` after remove).
- `useReviewShortcuts` is built on Nuxt UI's `defineShortcuts`, not hand-rolled
  `addEventListener` code (except one global keydown listener inside
  `features/review/components/CvViewer.vue`).
- No debounce, no localStorage, no intersection observers, no timers found in the
  client — `useCandidateFilter` is computed-only.
- `zod` is imported in exactly one file: `src/features/vacancies/validation.ts` (plus
  whatever `<UForm :schema>` consumes transitively).
- `vue-pdf-embed` is imported statically in `CvViewer.vue`, but that file only ships in
  the lazy review-route chunk.

## Findings

### TanStack Query (`@tanstack/vue-query`) — headline candidate

**What it provides (official Vue Query docs):** caching, deduping multiple requests for
the same data into one, background updates of stale data, structural sharing of results
(stable references for unchanged data), retries (3 with exponential backoff by
default), garbage collection of inactive queries after 5 minutes, and
`invalidateQueries` from mutation `onSuccess` callbacks for refresh-after-write. It
installs as `app.use(VueQueryPlugin)` and is compatible with Vue 3.x (peer range
`^2.6.0 || ^3.3.0`, so Vue 3.5 is fine).

**How it would map to this repo:**

- `useVacancyDetail`'s request-token stale-response guard disappears — keyed queries
  (`['vacancy', vacancyId]`) and reactive key refs handle route-param changes natively.
- The manual refresh coordination in `useVacancyDetailFlow`
  (`Promise.all([load(), loadCandidates()])` after import, `await load()` after delete)
  becomes `queryClient.invalidateQueries({ queryKey: ['vacancy', id] })` plus a
  candidates-key invalidation in the mutation's `onSuccess` — the exact pattern in the
  official "Invalidations from Mutations" guide.
- The `loading|error|empty|ready` view-state unions survive: they can be computed over
  `useQuery`'s `status`/`data` inside the same composables, so the page composition
  contract and seam specs keep their shape.

**Testing story (official testing guide):** tests wrap the unit under test in a fresh
`QueryClient` per test (isolation requirement), turn retries off via
`defaultOptions: { queries: { retry: false } }`, and await `waitFor` on query status.
Crucially for this repo, TanStack Query does not abstract the network layer — the
`queryFn` still calls the existing `api.ts` functions over `shared/http.ts`, so
`vi.stubGlobal('fetch', ...)` keeps working underneath. The official guide demonstrates
network mocking with nock for React; the equivalent here stays the repo's existing
fetch stub. Extra test setup cost: each spec mounts with a fresh `QueryClient` plugin
and `flushPromises()` still applies.

**Honest verdict — defer, not adopt now.** The real hand-rolled pain in this repo is
small and correct: one stale-token guard, two manual refresh coordination points, no
polling, no pagination, no shared cross-page cache, three routes. Adopting TanStack
Query would rewrite every `use*` composable and every seam spec's setup for benefits
(request dedup, retries, background refetch) the app does not currently need. It
becomes the right call the moment a second page needs the same cached server state
(e.g. a dashboard reading vacancy rollups already fetched by the list), or when
polling/background sync enters a spec — that is the earned-shared-code rule applied to
a dependency. Skipping now costs nothing; the composable seam (`api.ts` functions as
plain fetch promises) is exactly the shape a later `useQuery` adoption would plug into.

Sources:

- [Vue Query overview](https://tanstack.com/query/latest/docs/framework/vue/overview): server-state motivation and feature list (caching, dedup, structural sharing).
- [Vue Query installation](https://tanstack.com/query/latest/docs/framework/vue/installation): `npm i @tanstack/vue-query`, `VueQueryPlugin`, Vue 2/3 compatibility.
- [`@tanstack/vue-query` package.json](https://github.com/TanStack/query/blob/main/packages/vue-query/package.json): peer dependency `vue: ^2.6.0 || ^3.3.0`, `sideEffects: false`, deps on `@tanstack/query-core` + `vue-demi`.
- [Invalidations from mutations](https://tanstack.com/query/latest/docs/framework/vue/guides/invalidations-from-mutations): `useMutation` + `queryClient.invalidateQueries` after success — the direct replacement for `useVacancyDetailFlow`'s manual refresh.
- [Important defaults](https://tanstack.com/query/latest/docs/framework/react/guides/important-defaults) (Vue docs ref this page): cached data stale by default, 3 silent retries with backoff, refetch on window focus/reconnect, 5-minute GC of inactive queries, structural sharing.
- [Testing guide](https://tanstack.com/query/latest/docs/framework/react/guides/testing): fresh `QueryClient` per test, disable retries in tests, mock the network layer under the `queryFn`.

### VueUse (`@vueuse/core`)

VueUse is a collection of Composition API utilities (v12+ is Vue 3 only). The
replacement value in this client is thin by inspection: there is no hand-rolled
debounce, localStorage, mouse/sensor, or observer logic to replace. Keyboard shortcuts
are already covered by Nuxt UI's `defineShortcuts` (ADR-0007). The one genuinely
hand-rolled piece — the stale-response token in `useVacancyDetail` — is a data-fetching
concern that VueUse's `useFetch` would address, but `useFetch` is a strictly weaker
version of the TanStack Query decision above and inherits the same "not yet earned"
verdict. The single `window.addEventListener` in `CvViewer.vue` is three lines with a
matching `removeEventListener`; `useEventListener` saves nothing structural.

**Verdict: deliberately skip for now.** Add it piecemeal (tree-shakable, per-function
imports) the first time a real need appears — e.g. `useDebounceFn` if the candidate
search box moves from instant computed filtering to debounced input.

Sources:

- [VueUse Get Started](https://vueuse.org/guide/): Vue 3-only from v12, per-function imports from `@vueuse/core`, function categories (State, Browser, Sensors, Network, Watch).

### Pinia

Pinia is the official Vue store library: shared state across components/pages with
devtools, plugins, and typed stores. This repo's rule is explicit: flow state lives in
composables; "a store is earned by a second feature consumer" (AGENTS.md). By
inspection, every current async composable (`useVacancies`, `useVacancyDetail`,
`useCandidates`, `useReview`, `useIntakeRounds`) is consumed by exactly one page flow;
cross-feature composition (e.g. `useVacancyDetailFlow` composing `useCandidates`) goes
through composable arguments, not shared singleton state. There is no 2+ consumer state
today, and if it appears, a module-level composable is the documented first step in Vue
itself for an SPA without SSR.

**Verdict: deliberately skip.** Revisit only when a state slice genuinely has two
independent feature consumers — and even then, per the earned-shared-code rule, a
shared composable is the rung before Pinia.

Sources:

- [Pinia introduction](https://pinia.vuejs.org/introduction.html): what Pinia adds (devtools timeline, plugins, SSR safety, testing utilities) and its own note that a simple exported `reactive` covers small SPAs.

### zod v4 (already adopted — optimization within it)

The repo uses `zod` 4.5 in one file, `features/vacancies/validation.ts`, as schemas for
`<UForm :schema>` — exactly the ADR-0007 amended pattern, and Zod's own docs confirm
this is the right-sized use. Zod 4's tree-shakable variant is published as `zod/mini`
(or standalone `@zod/mini`): function-style checks instead of chainable methods, ~64%
smaller on the docs' minimal benchmarks (5.91kb → 2.12kb gzipped for a trivial schema).
But Zod's official guidance is blunt: regular Zod is the right default; bundle size at
this scale (5–10kb) matters only for slow-mobile user bases, and the Mini API is more
verbose and less discoverable — the creator himself states a strong preference for the
standard API. An internal HR tool on office networks is the opposite of Mini's target
audience.

**Verdict: keep standard `zod`; do not switch to `zod/mini`.** The validation.ts
pattern (pure input rules, server owns business rules) is already the intended usage.

Sources:

- [Zod Mini](https://zod.dev/packages/mini): tree-shaking benchmarks, the `.check()` function API, and the explicit "you should probably use regular Zod unless you have uncommonly strict constraints around bundle size" guidance.
- [Zod introduction](https://zod.dev/): feature list, 2kb core claim, Zod 4 stable.

### vue-pdf-embed (already adopted — check for load-time cost)

The component wraps pdf.js (currently pdfjs-dist 6.2.x per the repo's commit history).
Official repo notes: no peer dependencies; the PDF.js web worker is bundled as a blob
URL by default (works out of the box, but CSP configs blocking blob workers need the
"essential" build plus a manually configured `GlobalWorkerOptions.workerSrc`); it
cannot be server-rendered (irrelevant here, pure SPA); and a `usePdfDocument`
composable exists for loading a document once and sharing it across component
instances. Bundle cost: pdf.js is the heavy part, but in this app `vue-pdf-embed` is
imported only by `CvViewer.vue`, which lives exclusively inside the lazy-loaded review
route — Vite already splits it out of the initial bundle. No dynamic import is
warranted beyond what the router already provides. The seam spec already stubs the
component (`vi.mock('vue-pdf-embed', ...)`) because pdf.js cannot run under jsdom; that
stub stays regardless.

**Verdict: keep as-is.** Only act if a CSP is introduced (switch to essential build +
explicit workerSrc) or if the same document is ever rendered in two viewers at once
(adopt `usePdfDocument`).

Sources:

- [vue-pdf-embed README](https://github.com/hrynko/vue-pdf-embed): features, worker blob-URL default and CSP caveat with the essential-build workaround, `usePdfDocument`/`usePdfSearch` composables, SSR limitation, current pdfjs-dist version in its history.

### Vite 8 build configuration

The repo is on Vite 8, whose official build docs now describe Rolldown as the bundler
(`build.rolldownOptions`, `output.codeSplitting` for chunk strategy). Findings against
the current setup:

- **Lazy routes: already done** — all three routes use dynamic imports, so route-level
  splitting (including the pdf.js chunk) is automatic.
- **build.target: leave the default** — Vite 8's default target is Baseline Widely
  Available (Chrome/Edge ≥111, Firefox ≥114, Safari ≥16.4); an internal HR tool needs
  nothing lower, and lowering it only adds transform cost.
- **manualChunks / codeSplitting: skip** — with three lazy routes and one heavy vendor
  (pdf.js) already isolated by the router seam, hand-tuning chunks is speculative
  complexity. Revisit only with a measured bundle problem.
- **`vite:preloadError` handling: small, worthwhile.** The official docs show a
  3-line listener that reloads the page when a stale chunk fails to load after a new
  deployment — a real failure mode for any deployed SPA with hashed assets and lazy
  routes. This is the only build-adjacent item worth adopting now, and it needs no new
  dependency.
- Bundle visualization (e.g. rollup-plugin-visualizer / rolldown equivalents): not
  needed until someone is optimizing a measured problem; oxlint-only tooling culture
  (ADR-0001) argues against adding analysis tooling without a defect to hunt.

Sources:

- [Vite Building for Production](https://vite.dev/guide/build): default Baseline browser targets, `build.rolldownOptions`, `output.codeSplitting` chunking, and the `vite:preloadError` event with the reload recipe.

### Server-side (.NET 10) — short section

One candidate clears the bar from Microsoft Learn: **HybridCache**
(`Microsoft.Extensions.Caching.Hybrid`, in-box for ASP.NET Core 10) provides
`GetOrCreateAsync` with stampede protection, tag-based invalidation, and an in-process
primary cache with optional distributed secondary. It would fit read-heavy lookups that
change rarely (e.g. vacancy rollups) — but every such read in this app is a cheap
single-row Postgres query, and cache invalidation would add a second correctness
surface to mutations that currently just write through. Output caching and built-in
validation were checked in spirit and are likewise unearned: endpoints are
per-user-scale CRUD, and validation already lives in FluentValidation per slice
(ADR-0004).

**Verdict: defer.** Adopt HybridCache only when a specific endpoint shows measured
database load; the tag-invalidation model (`RemoveByTagAsync`) maps cleanly onto this
app's vacancy/round/candidate hierarchy when that day comes.

Sources:

- [HybridCache in ASP.NET Core (Microsoft Learn)](https://learn.microsoft.com/en-us/aspnet/core/performance/caching/hybrid): registration (`AddHybridCache`), `GetOrCreateAsync` semantics with single-flight stampede protection, tag-based invalidation, in-process default storage, .NET 10 moniker.

### Redundancy check on existing dependencies

Nothing in `package.json` is replaceable by a platform feature or redundant:

- `@fontsource/geist` — the design-language font (ADR-0008); self-hosting beats a CDN
  for an internal tool.
- `@iconify-json/lucide` — required by Nuxt UI's `UIcon` on-demand icon strategy
  (ADR-0007).
- `vue-pdf-embed`, `zod`, `@nuxt/ui`, `tailwindcss`, `vue-router` — each has exactly
  one job and one consumer contract already documented in ADRs.
- `vite-plugin-vue-devtools` — dev-only convenience; zero runtime cost, keep.
- The prior redundancy sweep (removing eslint chain, vue-sonner, explicit
  `Microsoft.OpenApi`) is already recorded in ADR-0001, ADR-0007, and
  `docs/research/2026-09-01-aspnet-core-bundled-package-findings.md`.

## Recommendations

Ranked, tied to the repo's own rules:

**Adopt now**

1. **`vite:preloadError` reload listener** — three dependency-free lines from the
   official Vite docs that fix a real post-deploy failure mode for lazy-routed SPAs;
   the only finding that costs nothing and earns its keep immediately.

**Consider later (earned, not speculative)**

2. **TanStack Query (`@tanstack/vue-query`)** — adopt when a second page consumes the
   same cached server state, or polling/background sync enters a spec. The current
   `api.ts`-over-fetch seam and composable-per-flow structure are already the shape it
   plugs into; seam tests keep working because fetch stays the network layer. It would
   delete the stale-token guard in `useVacancyDetail` and the manual refresh
   coordination in `useVacancyDetailFlow` — but today that is ~15 lines of correct code
   against a library, a plugin, and new test setup for every spec.
3. **VueUse, per-function** — add `@vueuse/core` only at the first concrete need (most
   likely `useDebounceFn` for the candidate search input if it ever becomes debounced);
   nothing hand-rolled today maps to it.
4. **HybridCache (.NET)** — adopt for a specific endpoint only when database load is
   measured; tag invalidation fits the vacancy/round hierarchy when needed.

**Deliberately skip**

5. **Pinia** — no state slice has a second feature consumer; AGENTS.md's
   "a store is earned by a second feature consumer" rule is the deciding authority, and
   Pinia's own docs concede a module-level composable covers small SPAs.
6. **zod/mini** — Zod's own docs recommend regular Zod unless bundle size is
   uncommonly critical; an internal office-network tool is the anti-case, and Mini's
   API is worse DX for zero user-visible gain.
7. **manualChunks / build.target tuning** — lazy routes already isolate the only heavy
   chunk (pdf.js); the default Baseline target fits; tuning without a measured problem
   violates the repo's no-speculative-complexity stance (ADR-0001 spirit).
8. **No removals from package.json** — every current dependency has exactly one
   documented job; the redundancy sweeps already happened (ADR-0001, ADR-0007).
