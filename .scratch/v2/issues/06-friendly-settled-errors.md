# 06: Friendly errors for settled-data conflicts

**What to build:** One shared client mechanism that turns API problem responses into
friendly, actionable UX — replacing raw leaks like "API request failed with status 409".
A `problemMessage(error, fallback)` helper in `src/hr-sat.Client/src/shared/` maps the
ProblemDetails `title` code to `{ title, description, color }`. **Settled**-state codes
(`IntakeRounds.Closed`, `IntakeRounds.NoActiveRound`, `IntakeRounds.ActiveRoundExists`,
`Vacancies.Closed`, plus the promote codes from 03) render amber `warning`; unknown 409s
and other failures render red `error`. Every mutation composable routes errors through the
helper, keeping its own call-site fallback title; call sites that know entity names enrich
the copy ("Round 2 — Q3 wave is closed", not "id 42"). List/detail pages auto-reload the
affected flow's data after a settled-state conflict (the `onChanged` pattern); the review
workspace uses its existing non-destructive warning channel. Copy is client-owned
presentation; the server's technical `detail` stays for logs. No toast action buttons —
guidance lives in the copy; the action is reachable where the user already is.

**Copy table** (title → description):

| Code | Title | Description |
|---|---|---|
| `IntakeRounds.Closed` | "{round} is closed" | "Closed rounds are read-only. To keep working with its candidates, promote them into the active round." |
| `IntakeRounds.NoActiveRound` | "No active round" | "Open a round first, then promote these candidates into it." |
| `IntakeRounds.ActiveRoundExists` | "A round is already active" | "Close the active round before opening a new one." |
| `Vacancies.Closed` | "This vacancy is closed" | "Reopen the vacancy to record hire outcomes." |
| promote not-closed code | "Round isn't closed yet" | "You can only promote from closed rounds. Close it first, or pick another round." |
| unknown 409 | "Couldn't save that change" | "The data changed on the server. We've refreshed — try again." |
| non-409 fallback | "Something went wrong" | "Please try again." |

**Blocked by:** nothing; covers the promote 409s from 03

**Status:** ready-for-agent

- [ ] `problemMessage(error, fallback)` in `src/hr-sat.Client/src/shared/` maps
  ProblemDetails codes → `{ title, description, color }` per the copy table
- [ ] Settled-state codes render `warning` (amber); unknown 409 and other failures render
  `error` (red)
- [ ] Call sites enrich copy with entity names where known (round display name, vacancy
  title)
- [ ] All mutation composables (intake rounds, candidates, import, vacancies; promote when
  built) route errors through the helper — no hand-written `error.message` descriptions
  survive
- [ ] List/detail pages reload the affected flow's data after a settled-state conflict
- [ ] Review workspace surfaces it through its existing warning channel, without forced
  navigation
- [ ] Seam tests: dialog stays open on 409, toast copy per table, reload happens; no raw
  "API request failed" text anywhere user-visible
