# Vue Client Development

This guide applies to every change under `hr-sat.client/`, including Vue single-file
components, composables, API clients, shared UI, routing, configuration, and client tests.
It supplements the generic Vue guidance in `.agents/skills/`; repository decisions in this
guide and `docs/agents/workflow.md` are the local source of truth when they differ.

## Start Here

1. **Establish context.** Read `CONTEXT.md`, the relevant files in `docs/adr/`, and the
   feature's user story or current spec before changing client behavior. Follow the ticket
   order in `docs/agents/workflow.md`: backend slice, frontend feature folder, then tests.
   This step is complete when the requested behavior and its API contract are identified.

2. **Load Vue guidance.** For every Vue task, read `.agents/skills/vue-best-practices/SKILL.md`
   and keep its required references in working context:
   `references/reactivity.md`, `references/sfc.md`, `references/component-data-flow.md`,
   and `references/composables.md`. Read `.agents/skills/vue/SKILL.md` when using Vue 3.5
   APIs or built-in components. Read `.agents/skills/vue-router-best-practices/SKILL.md`
   for route changes, guards, params, or route lifecycle work. Read
  `.agents/skills/vue-testing-best-practices/SKILL.md` for client tests. Check
  `.agents/skills/vueuse-functions/SKILL.md` before writing bespoke browser, DOM, storage,
  async, event, or utility plumbing; use a matching VueUse composable when its dependency
  and invocation rules permit, and read that function's reference before use. This step
  is complete when the references for every touched branch have been read.

3. **Map the component boundary.** For a non-trivial feature, write a brief working map
   before implementation: one responsibility for each component, its typed props and emits,
   and the state or side effects owned by each composable. Keep route views as composition
   surfaces. This step is complete when each new or changed responsibility has one named
   owner and one explicit data-flow contract.

## Repository Rules

### Architecture

- Put feature code in `hr-sat.client/src/features/<feature>/`: route view, feature
  components, composables, and API client.
- Keep `hr-sat.client/src/shared/` as the thin shared kernel for reusable UI, HTTP, and
  validation helpers. Feature state and feature-specific API behavior stay in the feature
  folder.
- Use Vue 3 Composition API with `<script setup lang="ts">` for new components. Keep
  Options API and untyped JavaScript out of new client code.
- Keep route-level views thin: compose the feature, connect routing, and pass contracts;
  put data loading, mutations, and side effects in composables or feature services.
- Split a component when it owns orchestration plus substantial presentation, contains
  three or more independent UI sections, or repeats a template block. For non-trivial
  CRUD or list features, separate the container, form, list or item, and status or action
  responsibilities unless the feature is demonstrably a tiny throwaway.

### Reactivity and data flow

- Keep source state minimal and derive display state with `computed`. Keep computed getters
  pure; use `watch` or lifecycle hooks for side effects only.
- Prefer `shallowRef` when deep reactivity is unnecessary or a value is replaced as a whole.
  Use `ref` or `reactive` when nested mutation must be observed, and avoid destructuring a
  `reactive` object in a way that disconnects its reactivity.
- Keep composable APIs small, typed, and organized by feature concern. Keep pure formatting
  and transformation helpers as plain utilities. Return read-only state when consumers must
  update it through explicit actions.
- Use props down and events up as the default. Treat props as read-only, type
  `defineProps` and `defineEmits`, and use `defineModel` only for a genuine two-way binding.
  Use typed symbol keys for provide/inject when a dependency must cross a deep component
  tree; keep mutations in the provider.
- Use `useTemplateRef()` for DOM or component refs on Vue 3.5+, and reserve imperative
  component refs for APIs that cannot be expressed with props and events.

### SFCs and templates

- Keep each component in one `.vue` SFC with sections ordered as `<script>`, `<template>`,
  then `<style>`.
- Use PascalCase filenames and component references. Keep templates declarative; move
  filtering, sorting, class logic, and other derivations into script-side computed values.
- Use stable primitive keys for `v-for`, keep `v-if` and `v-for` on separate elements, and
  use `v-if` or `v-show` according to mount cost and toggle frequency.
- Render user or server content with interpolation. Use `v-html` only for content that has
  an explicit trusted and sanitized source.
- Use `<style scoped>` and class selectors for component styles. Keep resets, typography,
  design tokens, and app-wide rules in `src/style.css`; use `:deep()` only at a deliberate
  component boundary and preserve established shared-component patterns.
- Reuse primitives from `src/shared/ui/` before adding a parallel button, dialog, field, or
  icon pattern. Keep accessibility behavior, keyboard interaction, loading states, and
  disabled states part of the component contract.

### API, routing, and tests

- Keep API calls in the feature's API module or composable, use the shared HTTP helpers,
  and preserve the typed server contract. Components should consume typed state rather than
  constructing request details inline.
- When a route parameter changes without leaving the route component, explicitly handle the
  new parameter and clean up any listeners or async effects. Use the router skill for guard,
  parameter, and lifecycle decisions.
- Follow the project testing stance: implement the slice first, then test the feature
  component seam with Vitest, Vue Test Utils, and jsdom. Assert user-visible states and
  emitted behavior rather than private implementation details or snapshots alone.
- Cover the relevant loading, error, empty, ready, disabled, and submission states. For
  teleported dialogs, assert against `document.body` and clear teleported content between
  tests.
- Oxlint is the sole client linter. Keep type checking with `vue-tsc` and use the scripts in
  `hr-sat.client/package.json` for linting, type checking, tests, and builds; do not add a
  second lint stack without revisiting ADR 0001.

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