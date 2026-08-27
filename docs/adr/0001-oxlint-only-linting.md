# ADR 0001: Oxlint as the sole client linter

## Status

Accepted (2026-08-27)

## Context

The Visual Studio ASP.NET + Vue template ships a dual-lint setup: eslint (flat config
with `@vue/eslint-config-typescript` + `eslint-plugin-vue`) runs in series with oxlint,
bridged by `eslint-plugin-oxlint` whose only purpose is disabling the rules both tools
implement. The chain needs `npm-run-all2` (`run-s`/`run-p`) and `jiti` (to load the
TypeScript eslint config) — seven devDependencies and two config files for one job.

This is a single-maintainer repo. Linting exists to catch real defects early, not to
enforce a house style guide. Deep-module terms: two adapters at one seam, and the second
adapter's interface (config surface) was nearly as large as its value.

## Decision

Oxlint is the only client linter. `npm run lint` is `oxlint . --fix`, driven by
`.oxlintrc.json` with the `vue` plugin enabled (it lints the `<script>` blocks of `.vue`
SFCs). Type-aware checking stays with `vue-tsc` in the `build`/`type-check` scripts.

Removed: `eslint`, `@vue/eslint-config-typescript`, `eslint-plugin-vue`,
`vue-eslint-parser`, `eslint-plugin-oxlint`, `jiti`, `npm-run-all2`, `eslint.config.ts`.

## Consequences

- One lint command, one config file; eslint's chain and bridge are gone.
- Given up, knowingly: template-block linting (`vue/attributes-order`,
  `vue/html-self-closing` and similar) — oxlint does not lint `<template>`. If template
  discipline becomes a real source of bugs, revisit this ADR rather than quietly
  re-adding eslint.
- Given up: stylistic rule breadth (perfectionist/import-order/padding rules). Not missed
  for a one-maintainer codebase.
- `oxlint` pins `~1.80.0`; upgrade deliberately since rule sets shift between minors.
- Future architecture reviews should not re-suggest "migrate to eslint" without a
  concrete defect class that oxlint demonstrably misses.
