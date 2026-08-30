# Workflow — How this repo builds features

Standing decisions for this project. The engineering skills read this file before planning
or implementing.

## Ticket shape

- **Vertical slices only**: one ticket cuts a narrow but complete path through every layer
  (schema, API, UI, tests). No horizontal tickets ("all backend", "all frontend").
- One feature = one ticket, sized for a single fresh context window.

## Work order inside a ticket

Each ticket is worked in this order:

1. **Backend slice** — the feature's endpoint, handler, and persistence (VSA slice in the
   ASP.NET Core monolith, Clean Architecture layers).
2. **Frontend feature folder** — the Vue feature's components/composables/API client under
   `src/features/<feature>/`.
3. **Tests** — written after the implementation, at the seams declared in the spec.

## Testing stance

- **No TDD.** Tests come after implementation within the same ticket. This is an explicit
  deviation from the default `/implement` behaviour, chosen for this project.
- What gets tested — flow-first scope, traceability, and the critical spine — is defined
  by `docs/agents/testing.md` (ADR 0003). Backend tests at the HTTP API seam; frontend
  tests at the feature-component seam.

## Architecture posture

- **Backend**: ASP.NET Core monolith, Vertical Slice Architecture (one folder per feature)
  over Clean Architecture layers (Domain / Application / Infrastructure / API host).
- **Frontend**: Vue SPA, feature-folder VSA (`src/features/`, `src/shared/`).
- Deploy with Docker per `docs/discovery/04-architecture.md` (MVP V1 diagram). The
  queue/worker "maybe final" design is out of scope until a spec says otherwise.

## Sources of truth

- Backlog: `docs/discovery/03-user-stories.md`
- UI behaviour: `docs/discovery/05-ui-sketches.md`
- Current plan: `.scratch/mvp/spec.md` + `.scratch/mvp/issues/`
