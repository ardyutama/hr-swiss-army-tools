# 06: Friendly lifecycle-conflict errors

**What to build:** Replace raw client error leaks with one pure ProblemDetails-to-UX mapper and
route every composable catch through it. The server's technical `detail` remains unused in
rendered copy. Validation problems keep their existing field-error branches before the mapper.

## Domain language

The canonical term is **Lifecycle Conflict**. It covers a refusal because a record's lifecycle
state forbids the operation: changing settled data or failing a lifecycle precondition. Lifecycle
conflicts are expected and actionable, so they use amber warnings. Unknown conflicts and technical
failures use red errors.

## Helper contract

Add `problemMessage(error, fallback, options?)` in
`src/hr-sat.Client/src/shared/problem-details.ts`. It is pure and returns:

```ts
{
  title: string
  description: string
  color: 'warning' | 'error'
  kind: 'lifecycle' | 'conflict' | 'failure'
}
```

`options` is `{ round?: string; conflictDescription?: string }`.

| Match | Title | Description | Color / kind |
|---|---|---|---|
| `IntakeRounds.Closed` | `"{round} is closed"` or `"This round is closed"` | `"Closed rounds are read-only. To keep working with its candidates, promote them into the active round."` | warning / lifecycle |
| `IntakeRounds.NoActiveRound` | `"No active round"` | `"Open a round first, then promote these candidates into it."` | warning / lifecycle |
| `IntakeRounds.ActiveRoundExists` | `"A round is already active"` | `"Close the active round before opening a new one."` | warning / lifecycle |
| `IntakeRounds.NotClosed` | `"{round} isn't closed yet"` or `"This round isn't closed yet"` | `"You can only promote from closed rounds. Close it first, or pick another round."` | warning / lifecycle |
| `Vacancies.Closed` | `"This vacancy is closed"` | `"Reopen the vacancy to record hire outcomes."` | warning / lifecycle |
| unknown 409 | `"Couldn't save that change"` | `"The data changed on the server. We've refreshed -- try again."` | error / conflict |
| unknown 409 with `conflictDescription` | `"Couldn't save that change"` | supplied description | error / conflict |
| 404 | `"Couldn't find that"` | `"It may have been removed. Head back and reopen it."` | error / failure |
| anything else | call-site `fallback` | `"Please try again."` | error / failure |

Only `IntakeRounds.Closed` and `IntakeRounds.NotClosed` use `options.round`. The generic round
copy is used everywhere else. `Vacancies.Closed` remains generic.

## Routing and reload rules

- Every catch in every composable routes through the helper; loads use its copy in their rendered
  error state and never toast.
- Validation 400/422 branches in import, promote, and vacancy save run before the helper and keep
  their existing field-error behavior.
- Any 409 reloads the affected flow through the existing `onChanged` seam or its own `load()`.
  404, 500, and network errors never reload.
- Dialogs stay open on 409. A toast may fire and the user dismisses the dialog manually.
- Review never reloads. Lifecycle conflicts go to a new `conflictWarning` ref and the existing
  amber toast watcher; the user stays on the candidate. Unknown conflicts and failures stay in
  the existing inline `p[role="alert"]` channels.
- Review overrides unknown-409 copy with: `The data changed on the server. Go back and reopen
  this round to see the latest, then try again.`

## Acceptance matrix

1. Vacancy-detail: opening while another round is active shows the amber lifecycle toast, keeps
   the dialog open, and reloads.
2. Vacancy-detail: promoting from a not-yet-closed round shows the enriched round name, keeps the
   dialog open, and reloads.
3. Vacancy-detail: promoting with no active round shows the lifecycle warning.
4. Review: setting an outcome on a closed vacancy shows an amber warning and stays on the
   candidate.
5. Review: deciding on a candidate whose round closed shows the generic closed-round warning and
   stays on the candidate.
6. Review: a 500 on save renders `Something went wrong` and `Please try again.` inline.
7. One flow proves unknown 409 copy and reload behavior.
8. Vacancy list and vacancy detail load-500 tests render `Something went wrong` instead of the
   raw API message.
9. No spec or rendered UI contains `API request failed`.

**Status:** ready-for-agent

- [ ] Rewrite the ticket and glossary with Lifecycle Conflict language.
- [ ] Add the pure helper and migrate every composable catch.
- [ ] Preserve validation-first branches and 409-only reload behavior.
- [ ] Add or update the flow tests at the view seams above.
- [ ] Run client `test`, `type-check`, and `lint` from `src/hr-sat.Client/`.
- [ ] Do not commit; the user pushes the branch.
