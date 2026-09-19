# 03 layout proposal: Screening rules editor + screened-out list treatment

Design read: dense internal tool (settings panel + list treatment), not a marketing
surface — the design-taste skill's landing-page rules are out of scope here (its own
Section 13), so this follows ADR-0008's warm-utilitarian language: light-only, Geist,
radius 12px, one accent (`--ui-primary`, `#4361a8`), density 5–6, motion 3–4, Nuxt UI v4
components only. Amber stays reserved for the Flagged review state and Lifecycle
Conflicts, so the screened-out surfaces below (banner, toggle, chips) are **neutral**;
amber appears in exactly one place: the per-rule all/nothing warning in the editor,
which is the ADR-0014 safety net.

Companion to [03-screening-rules.md](03-screening-rules.md) — grill decisions 9–17 pin
the behavior; this pins the shape. No code yet.

## Where it lives

1. **Vacancy detail, "Screening" section** — a settings-area section beside Form Layout:
   a read-only summary line ("2 rules · 37 screened out in this round", or the empty
   state) plus a ghost "Screening rules" button opening the editor modal. No new route.
2. **Candidate list toolbar** — the screened-out toggle with its count badge.
3. **Review workspace** — the neutral banner on a screened-out candidate.

## The editor (UModal)

`UModal`, size `lg`, scrollable body. One rule per row-block; value input hidden for
`is-empty` / `not-empty`.

```
┌ Screening rules ────────────────────────────────────────────────────────────┐
│ Screen out form candidates whose raw answers fail these rules. Rules read   │
│ the stored CSV text as-is — no number or date parsing — and editing them    │
│ re-sorts candidates instantly. Screening never deletes anyone.              │
│                                                                             │
│  Rule 1                                                                     │
│  Column   [ 10 · Bersedia ditempatkan di departemen mana saja?        ▾ ]   │
│  Operator [ equals                                                    ▾ ]   │
│  Value    [ Tidak_____________________________________________________ ]    │
│                                                             [ Remove rule ] │
│                                                                             │
│  Rule 2                                                                     │
│  Column   [ 11 · Pengalaman kerja                                     ▾ ]   │
│  Operator [ is empty                                                  ▾ ]   │
│                                                             [ Remove rule ] │
│                                                                             │
│  [ + Add rule ]                                             2 of 5 rules    │
│                                                                             │
│  ─────────────────────────────────────────────────────────────────────────  │
│  Would screen out 41 of 298 form candidates in the active round.            │
│  ⚠ Rule 2 screens out everyone — check the column.                          │
│                                                         [ Cancel ]  [ Save ]│
└─────────────────────────────────────────────────────────────────────────────┘
```

Behavior notes:

- **Column select** lists every column of the header snapshot as `ordinal · header`
  (header truncated ~48 chars), with the Column Label shown when one exists. Options
  come from the layout's snapshot — rules are independent of picked columns (decision
  2), so picked state never filters this list.
- **Operator select**: `equals` / `not-equals` / `is empty` / `is not empty` /
  `contains`. Choosing `is empty` / `is not empty` hides and clears the value input.
- **Add rule** appends at the bottom; no reordering (decision 16). Disabled at 5 with
  the counter carrying the explanation ("5 rules at most").
- **Footer count** is the preview endpoint (decision 12): fetched on open and debounced
  (~300ms) on any edit. "N of M" — M is the active round's **form** candidates only.
- **Per-rule amber warning** renders when a rule's preview count is 0 or M:
  "Rule 2 screens out everyone — check the column." / "Rule 1 matches no one — it will
  never fire." This is the only amber on these surfaces.
- **No active round**: the count line is replaced by "No active round — saved rules
  apply to the next round's imports." Editing and saving still work (rules bind future
  imports).
- **Closed vacancy**: the modal opens read-only — inputs disabled, Save hidden.
- **Before the first import** (no header snapshot): the section's summary line is the
  empty state — "Import form responses to set up screening rules." — and the button is
  disabled. Rules need snapshot columns to point at (decision 2).
- **Validation** via `<UForm :schema>` with a Zod schema in the feature's
  `validation.ts`: ≤5 rules, value required and non-empty for `equals` / `not-equals` /
  `contains`, value absent otherwise. Server owns the same invariants (decision 1);
  client errors render inline.

## The list treatment

Toolbar gains the toggle at the trailing end, after the existing status/outcome/search
controls:

```
  [ All statuses ▾ ] [ Any outcome ▾ ] [ Search…          ]    ☐ Show 37 screened out
```

- The badge count is `counts.screenedOut` from the paged list response — always
  present, independent of active filters (decision 10), so the number is stable while
  HR filters.
- Toggling sets `screened=all` in the route query and refetches (page resets to 1).
- Screened rows render their fired-rule chips in a trailing column; each chip is the
  server-rendered `display` string (decision 9):
  `Bersedia di departemen mana saja · equals "Tidak"`. Chips are neutral gray.
- Screened rows show **no delete affordance** (decision 7).
- The empty-filtered state copy distinguishes "no candidates match these filters" from
  "all candidates are screened out — use the toggle to see them" when
  `counts.screenedOut > 0` and the page is empty with the toggle off.

## The review-page banner

Top of the review workspace, above the evidence panels, for a screened-out candidate:

```
┌─────────────────────────────────────────────────────────────────────────────┐
│ Screened out by 2 rules:  [ Dept. mana saja · equals "Tidak" ]              │
│                           [ Pengalaman kerja · is empty ]                   │
│ Screening never decides for you — review, flag, or shortlist as usual.      │
└─────────────────────────────────────────────────────────────────────────────┘
```

Neutral (gray), not amber. Data comes from the `screening` field on
`GetCandidateDetails` (decision 13); nothing is computed client-side.

## Promote dialog

Eligible screened-out candidates appear with a "screened out" chip on their row
(decision 11) — promotable like any other; the frozen verdict is dropped on promote
and the candidate re-evaluates live in the active round.
