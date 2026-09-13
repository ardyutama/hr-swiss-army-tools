# Vue Client Development

This guide applies to every change under `src/hr-sat.Client/`. The living client rules are
owned by [`.agents/skills/vue-feature-slices/SKILL.md`](../../.agents/skills/vue-feature-slices/SKILL.md)
(ADR-0011); this guide is a thin pointer carrying only the workflow below and the
repo-specific deltas the skill does not. UI decisions are recorded in ADR-0007 (Nuxt UI)
and ADR-0008 (design language); testing scope in ADR-0003.

## Start Here

1. **Establish context.** Read `CONTEXT.md`, the relevant files in `docs/adr/`, and the
   feature's user story or current spec before changing client behavior. Follow the ticket
   order in `docs/agents/workflow.md`: backend slice, frontend feature folder, then tests.
   This step is complete when the requested behavior and its API contract are identified.

2. **Load the slice rules.** Read
   [`.agents/skills/vue-feature-slices/SKILL.md`](../../.agents/skills/vue-feature-slices/SKILL.md)
   — the client source of truth for slice structure, state ownership, import discipline,
   and the placement test. This step is complete when you can name the feature folder and
   the public surface the change touches.

3. **Map the component boundary.** For a non-trivial feature, write a brief working map
   before implementation: one responsibility for each component, its typed props and emits,
   and the state or side effects owned by each composable. Keep route views as composition
   surfaces. This step is complete when each new or changed responsibility has one named
   owner and one explicit data-flow contract.

## Repository Rules

The living rules for slice structure, state ownership, reactivity, SFC/template
discipline, API/routing, and the placement test are owned by
[`.agents/skills/vue-feature-slices/SKILL.md`](../../.agents/skills/vue-feature-slices/SKILL.md)
(ADR-0011). This section carries only the repo-specific deltas the skill does not.

- Keep the project name `hr-sat.Client` with PascalCase `Client`; use kebab-case for client
  feature and page directories and PascalCase for Vue component filenames.
- UI and styling: Nuxt UI v4 is the component source of truth and Tailwind CSS v4 the
  styling system — import `U*` components directly, compose from Nuxt UI primitives, use
  Iconify lucide icons via `UIcon`, and keep `UApp` at the app root. No `App*` wrapper
  layer and no resurrected `src/shared/ui/` (ADR-0007). Design tokens and the locked
  palette live in `@theme` in `src/style.css` (ADR-0008).
- Form validation uses `<UForm :schema>` with Zod schemas kept in the feature's
  `validation.ts` (ADR-0007, amended).
- Oxlint is the sole client linter; type checking stays with `vue-tsc`. Use the scripts in
  `hr-sat.Client/package.json` for lint, type-check, test, and build; do not add a second
  lint stack without revisiting ADR 0001.

### Tests

Follow the project testing stance in `docs/agents/testing.md` (ADR 0003): implement the
slice first, then test the feature-component seam with Vitest, Vue Test Utils, and jsdom
as Flow Tests traced to a user story or glossary term. The binding mechanics:

- Mock only `fetch` via `vi.stubGlobal`, plus platform modules the component genuinely owns
  (e.g. `vi.mock('@nuxt/ui/composables/useToast')`). Everything else inside the seam runs
  for real.
- Query the DOM the way a user perceives it: visible text, labels, placeholders, and aria
  attributes. Add a `data-testid` only when no semantic handle exists.
- Assert user-visible outcomes: rendered content, toasts, and emitted events. Keep
  `wrapper.vm` internals, direct component method calls, and snapshot assertions out of
  specs — they break on refactors that preserve behavior.
- Await every interaction: `await trigger()` and `await setValue()`, and
  `await flushPromises()` after every mount or action that fires a fetch or other promise.
  Reserve `nextTick` for programmatic reactive changes.
- Reset the world in `afterEach`: `vi.clearAllMocks()`, `vi.unstubAllGlobals()`, and
  `document.body.innerHTML = ''` whenever a test rendered teleported content.
- For teleported dialogs (`UModal` and friends), query and assert against `document.body`;
  `wrapper.find()` cannot see teleported content.

## Completion Check

Before declaring a client change complete, verify all of the following:

- The changed components use Composition API, typed contracts, focused responsibilities,
  and the required SFC structure.
- State is minimal, derived values are computed, and side effects have an explicit owner
  with cleanup where needed.
- Feature code remains in its feature folder and shared code remains genuinely reusable.
- Client tests cover the changed behavior at the feature-component seam, including changed
  states and user interactions.
- The narrow client test or build check passes, followed by the relevant type-check and
  lint scripts. Report any pre-existing failure separately from the change.