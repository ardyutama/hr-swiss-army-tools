# 03 layout proposal: Screening rules editor + screened-out list treatment

Design read: dense internal tool (settings panel + list treatment), not a marketing
surface — the design-taste skill's landing-page rules are out of scope here (its own
Section 13), so this follows ADR-0008's warm-utilitarian language: light-only, Geist,
radius 12px, one accent (`--ui-primary`, `#4361a8`), density 5–6, motion 3–4, Nuxt UI v4
components only. Amber stays reserved for the Flagged review state and Lifecycle
Conflicts, so the screened-out surfaces below (banner, toggle, chips) are **neutral**;
amber appears in exactly one place: the per-rule all/nothing warning in the editor,
which is the ADR-0014 safety net.

Design-taste binding subset (grill decision 29; ADR-0008 amended the same date): what
binds here is full state cycles (loading/empty/error per region), inline errors never
toast-only, one-sentence-plus-one-action empty states, WCAG AA contrast on every
control, no dead UI, emoji-free UI (warning glyphs come from the icon set), the
shape/color/density locks, and the middle-dot ration (≤1 per line — the chip format
complies). What does not bind: the em-dash ban (the repo's copy voice predates the
skill — `Round 1 — First wave`, `Send email — <reason>`), hero/bento/marquee
choreography, and dark-mode dual design.

Companion to [03-screening-rules.md](03-screening-rules.md) — grill decisions 9–17 pin
the behavior; this pins the shape. No code yet.

## Where it lives

1. **Vacancy detail, "Screening" section** — a settings-area section beside Form Layout:
   a read-only summary line ("2 rules · 37 screened out in Round 2", or the empty
   state) plus a ghost "Screening rules" button opening the editor modal. No new route.
   The count clause counts the **viewed** round and names it; it drops when no round is
   selected, and never reuses the editor's "in the active round" phrasing (decision 28).
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
│  [!] Rule 2 screens out everyone — check the column.                        │
│                                                             [ Remove rule ] │
│                                                                             │
│  [ + Add rule ]                                             2 of 5 rules    │
│                                                                             │
│  ─────────────────────────────────────────────────────────────────────────  │
│  Would screen out 41 of 298 form candidates in the active round.            │
│                                                   [ Cancel ]  [ Save rules ]│
└─────────────────────────────────────────────────────────────────────────────┘
```

Behavior notes:

- **Column select** lists every column of the header snapshot as `ordinal · label`
  when a Column Label exists (label leads, ordinal disambiguates), else
  `ordinal · header` truncated ~48 chars. Options come from the layout's snapshot —
  rules are independent of picked columns (decision 2), so picked state never filters
  this list. A saved rule whose ordinal vanished from the snapshot (a drift
  confirmation shrank the header set) keeps its option rendered as "Column N — not in
  the current header snapshot": keepable or removable, never a validation block — the
  server still evaluates it (short-row = empty, decision 3) and the amber net flags
  the dead rule from the preview (decision 32).
- **Operator select**: `equals` / `not-equals` / `is empty` / `is not empty` /
  `contains`. Choosing `is empty` / `is not empty` hides and clears the value input.
- **Add rule** appends at the bottom; no reordering (decision 16). Disabled at 5 with
  the counter carrying the explanation ("5 rules at most").
- **Footer count** is the preview endpoint (decision 12): fetched on open and debounced
  (~300ms) on any edit. "N of M" — M is the active round's **form** candidates only.
- **Per-rule amber warning** renders **inline under the rule row it names** (not the
  footer — with five rows a footer "Rule 2" forces a mapping exercise; decision 27),
  as a compact warning UAlert with `i-lucide-triangle-alert` (emoji-free per the
  binding subset), when a rule's preview count is 0 or M:
  "Rule 2 screens out everyone — check the column." / "Rule 1 matches no one — it will
  never fire." This is the only amber on these surfaces.
- **Preview is advisory** (decision 27): latest-wins debounce, stale responses
  discarded; a failed preview renders "Live count unavailable" in the count region and
  never blocks Save — the server owns the invariants. Zod validation errors render
  inline under the rule row on submit, per the UForm precedent.
- **Save cascade** (decision 27): PUT → close → success toast → one refresh
  announcement reloads the candidate list and the vacancy progress header in parallel.
- **Footer buttons**: Cancel discards with no dirty-confirm; the save action reads
  "Save rules" (modal-atomic per the form-layout precedent).
- **No active round**: the count line is replaced by "No active round — saved rules
  apply to the next round's imports." Editing and saving still work (rules bind future
  imports).
- **Active round, no form candidates** (fresh round or email-only): the count line
  reads "No form candidates in the active round to screen." and per-rule amber is
  suppressed — 0 of 0 is not a signal (decision 32). The editor's third distinct count
  state, beside "no active round" and the pre-snapshot disabled state.
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
  [ All statuses ▾ ] [ Any outcome ▾ ] [ Search…          ]    [☐] Show screened out  37
```

- The toggle is a `UCheckbox` — "Show screened out" is subset-inclusion filter
  semantics, not a settings on/off (decision 25) — with the count as a **separate
  neutral `UBadge`** carrying `counts.screenedOut`: always present, independent of
  active filters (decision 10), stable while HR filters.
- At zero screened-out in the viewed round the checkbox renders **disabled** with a
  tooltip — "No screened-out candidates in this round." Unavailable-but-explained
  (the Send-button precedent), never hidden: the toolbar stays spatially stable as
  imports land (decision 30).
- Toggling sets `screened=all` in the route query and refetches (page resets to 1).
- The search placeholder adapts to the vacancy: "Name, email, or phone" when a form
  layout exists (typed contact details become searchable per decision 33), else the
  current "Name, sender email, or subject".
- Fired-rule chips stack **inside the Candidate cell**, under the name (decision 26):
  no new column, the colgroup never reflows on toggle, and the semantic table stays
  intact (ADR-0008 #10). Each chip is the server-rendered `display` string (decision 9)
  — `Bersedia di departemen mana saja · equals "Tidak"` — neutral gray, one line,
  truncated, full text in a `UTooltip`. At most 2 chips render; the rest collapse into
  a "+N more rules" chip whose tooltip lists all.
- Screened rows show **no delete affordance** (decision 7) but stay fully
  row-clickable into review — screening never blocks human action (decision 13); the
  Send action keeps following the existing contactability rules. Closed rounds render
  the identical treatment from the frozen verdict — no client special-casing.
- The empty-filtered state copy distinguishes "no candidates match these filters" from
  "all candidates are screened out — use the toggle to see them" when
  `counts.screenedOut > 0` and the page is empty with the toggle off.

## Pagination (new — sets the precedent)

`UPagination` below the table at the trailing end, with a leading "1-100 of 298" text
counter (plain text — density 5-6, no mono). `page` lives in the route query, omitted
when 1; any filter change resets it (decision 15). A round switch drops `page` and
only `page` — the other filters persist so HR compares rounds through the same lens
(decision 31). Between pages the toolbar and table
chrome hold still while shape-matched skeleton rows swap in (ADR-0008 #14) — no
full-panel spinner. A stale page (deep link, or reclassification shrank the set)
renders the standard empty state — "This page is empty — candidates may have been
reclassified." plus a "Back to page 1" action — never an auto-redirect (decision 24).

The toolbar wraps to two rows below `lg`, the toggle pinned trailing on row one — the
collapse is declared, not left to chance (shared office PCs at 1280px hit it).

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
`GetCandidateDetails` (decision 13); nothing is computed client-side. It stacks above
the Prior Application Notice when both apply (disposition outranks history) and is
non-dismissible — a persistent disposition, not a notification. Rendered as a neutral
`UAlert` per the PriorApplicationNotice precedent, icon `i-lucide-filter` or none —
never the triangle-alert glyph, which carries destructive weight on the action bar.
The review queue the banner sits in mirrors the list's URL state, `screened=all`
included, and round-trips `page` so Back restores the list exactly (decision 23).

## Promote dialog

Eligible screened-out candidates appear with a single neutral "screened out" badge
inline after the name on their row (decision 11) — not per-rule chips; the dialog is a
rescue path, not a diagnostic. Promotable like any other; the frozen verdict is
dropped on promote and the candidate re-evaluates live in the active round.
