# ADR 0007: Nuxt UI as the client component source of truth

## Status

Accepted (2026-09-01).

## Context

The client shipped a hand-rolled design system: nine bespoke wrappers in
`hr-sat.client/src/shared/ui/` (`AppButton`, `AppDialog`, `AppField`, `AppIcon`,
`AppShell`, `AppSidebar`, `ConfirmDialog`, `IconButton`, `StatCard`) styled by a custom
property design system in `hr-sat.client/src/style.css`, plus `vue-sonner` for toasts.
Every new flow component meant styling from scratch against those tokens, and the
wrappers re-implemented accessibility and interaction behavior (focus traps, keyboard
handling, ARIA wiring) that mature libraries already solve.

The alternatives considered:

- **Keep and grow the bespoke kit.** Zero migration cost, but every new primitive
  (table sorting, dropdown menus, form fields, slide-overs) is new bespoke code, and
  the accessibility burden stays ours.
- **Headless primitives only (reka-ui / radix-vue).** Full style control, but we would
  still hand-build every styled component — the same cost as today with an extra
  dependency.
- **Nuxt UI v4.** Ships 125+ styled, accessible components (on reka-ui + Tailwind CSS
  v4 + Tailwind Variants), officially supports plain Vite + Vue apps without Nuxt
  (`@nuxt/ui/vite` plugin, `app.use(ui)`, `<UApp>` wrapper), includes `<UForm>` with
  Standard Schema validation (Zod), `useToast`, `defineShortcuts`, and Iconify icons.
  MIT licensed.

## Decision

- Nuxt UI v4 is the single source of truth for UI components in `hr-sat.client/`.
  Feature and page code import `U*` components directly (via the plugin's
  auto-imports); no repo-level `App*` wrapper layer is reintroduced.
- Tailwind CSS v4 is the styling system. Design tokens live in `@theme` in
  `src/style.css`; the legacy `:root` custom properties are deleted once every slice
  migrates. Utility classes are allowed in templates.
- Form validation uses `<UForm :schema>` with Zod schemas in the feature folder; the
  feature `validation.ts` modules are retired.
- Toasts use Nuxt UI's `useToast()`; `vue-sonner` is removed. The app root is wrapped
  in `<UApp>` (required for toasts, tooltips, and overlays).
- Icons come from Iconify via `UIcon` (on-demand).
- Auto-import is kept enabled; the generated `components.d.ts` and `auto-imports.d.ts`
  are committed and included in `tsconfig.app.json`. Oxlint remains the sole client
  linter (ADR-0001 unchanged).
- `CONTEXT.md` is unchanged: this is an implementation decision, not domain language.
  Nuxt UI component names (`UModal`, `UForm`, …) must never appear in user-facing copy
  or leak into domain vocabulary — a purge confirmation stays a purge confirmation.

## Consequences

- The bespoke kit (`src/shared/ui/*`, most of `src/style.css`) is deleted, not
  deprecated; slices migrate one at a time (vacancies list first) and stay green at
  every step.
- The client gains Tailwind CSS v4, reka-ui, and Tailwind Variants as transitive
  dependencies, and auto-import magic becomes part of the toolchain (mitigated by
  committing the generated declarations so type-checking and linting stay explicit).
- New components are composed from Nuxt UI primitives instead of being built from
  tokens; bespoke CSS is justified only for layout unique to this app (e.g. the shell
  grid) until Nuxt UI layout components are adopted deliberately.
- Reverting this decision means restoring the token system and rewriting every
  migrated template — the migration is expected to be one-way.
