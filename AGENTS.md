# AGENTS.md
## Commit rules
Don't commit the changes automatically, I'll push myself.

## Architecture contract

Applies to every change, both tiers. One request = one **slice** (backend); one user flow =
one **slice** (frontend). Folders are feature-first. Shared code is **earned**: it moves to
`Shared/` or `src/shared/` only at its second same-reason consumer. A feature is complete as
a full vertical slice: schema → API → client → seam tests, in `docs/agents/workflow.md` order.

### Frontend slice layout

One user flow = one page under `src/hr-sat.Client/src/pages/<page>/` plus one feature module
under `src/hr-sat.Client/src/features/<feature>/`.

`src/pages/<page>/`:

- `<Flow>View.vue` — thin route view: composes feature modules and wires props/events;
  fetches, mutates, formats nothing
- `<Flow>View.spec.ts` — seam tests: mount the view, stub `fetch`, assert user-visible outcomes

`src/features/<feature>/`:

- `use<Flow>.ts` — one composable per async lifecycle; owns loading/error/data and the
  view-state union (`loading | error | empty | ready`)
- `api.ts` — typed DTOs plus one function per endpoint over `shared/http.ts`
- `validation.ts` — pure input rules; the server owns business rules
- `format.ts` — pure display formatting helpers
- `components/` — flow presentation with typed props/emits

Pages compose feature modules; features never import from pages or from a sibling
feature's internals (cross-feature imports use the feature's root modules only). Flow
state lives in composables; a store is earned by a second feature consumer. Full rules:
`docs/agents/vue.md` and `.agents/skills/vue-feature-slices/`.

## Agent skills

### .NET architecture and coding

For ASP.NET Core, EF Core, backend testing, dependency injection, domain modeling, or
performance work, read `docs/agents/dotnet.md` after `CONTEXT.md` and any relevant ADRs.
Backend slice conventions (layers, handlers, validation, errors, per-slice tests) are
owned by the `.agents/skills/vertical-slice-dotnet/` pack (`add-feature`, `add-entity`,
`add-tests`, `ca-review`) — the source of truth per ADR-0005; `docs/agents/dotnet.md`
adds only repo-specific rules. Use `.agents/skills/dotnet-skills/` as conditional
playbooks for the matching task.

### Vue client development

For client-side work in `src/hr-sat.Client/` (Vue SFCs, composables, router, shared UI, Vite
configuration, or frontend tests), read `docs/agents/vue.md`. It defines the repository's
frontend rules and points to the required Vue, router, and testing best-practice skills.
Nuxt UI v4 is the client UI source of truth: import its components directly, style with
Tailwind CSS v4, and keep `UApp` at the application root. Do not add `App*` UI wrappers or
resurrect the deleted `src/shared/ui/` layer; see ADR-0007.

### Issue tracker

Work items derive from `docs/discovery/03-user-stories.md` and live as local markdown under `.scratch/<feature>/`. See `docs/agents/issue-tracker.md`.

### Testing

Flow-first test scope, traceability, and the critical spine: see `docs/agents/testing.md` (ADR 0003).

### Triage labels

Default canonical labels (`needs-triage`, `needs-info`, `ready-for-agent`, `ready-for-human`, `wontfix`). See `docs/agents/triage-labels.md`.

### Domain docs

Single-context: one `CONTEXT.md` + `docs/adr/` at the repo root (created lazily). See `docs/agents/domain.md`.
