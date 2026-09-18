# 02 layout proposal: Form Layout panel

Design read: dense internal tool (settings panel + two dialogs), not a marketing surface —
the design-taste skill's landing-page rules are out of scope here (its own Section 13), so
this follows ADR-0008's warm-utilitarian language: light-only, Geist, radius 12px, one
accent (`--ui-primary`, `#4361a8`), density 5–6, motion 3–4, Nuxt UI v4 components only.
Amber stays reserved for the Flagged review state, so the drift dialog below is neutral,
not amber.

Companion to [02-form-layout-config.md](02-form-layout-config.md). No code yet; this pins
the shape so the backend contract and the client slice can be built against it.

## Where it lives

One panel, three entry points, per the issue's "same panel, guided mode" rule:

1. **Vacancy detail header** — a "Form layout" button (ghost, next to Import) opens the
   panel in **edit mode**. A read-only summary of the current mapping shows in the vacancy
   settings area so HR can see the mapping without opening anything.
2. **First .csv upload with no valid layout** — the import is refused (409), the held file
   is kept by the import dialog, and the same panel opens in **guided mode**: blocking,
   not dismissible via backdrop, and saving completes the import in one flow.
3. **Re-upload with changed mapped headers** — the import is refused (409, drift), and the
   **drift dialog** opens. "Re-map" escalates into the full panel.

## Edit mode (the panel)

`UModal`, size `xl`, scrollable body. Two zones: roles first (they pre-fill Candidate
Details), then display fields.

```
┌ Form layout ────────────────────────────────────────────────────────────────┐
│ Map this vacancy's Google Form export. Columns are matched by position,     │
│ never by header text, so mid-vacancy question edits cannot break anything.  │
│                                                                             │
│ Roles                                                                       │
│ Each role binds to at most one column and pre-fills Candidate Details.      │
│                                                                             │
│  Name *              [ 2 · Nama Lengkap                               ▾ ]   │
│                      Short label  [ Candidate name____________________ ]    │
│                      Header: Nama Lengkap (Full name)                       │
│                                                                             │
│  Contact email *     [ 4 · Email aktif                                ▾ ]   │
│                      Short label  [ Email_____________________________ ]    │
│                      Header: Email aktif yang bisa dihubungi                │
│                                                                             │
│  Contact phone       [ Not assigned                                   ▾ ]   │
│  CV link             [ 7 · Link CV (Google Drive)                       ▾ ]   │
│                                                                             │
│  ─────────────────────────────────────────────────────────────────────────  │
│                                                                             │
│  Form answers on the review page                              Picked 4 / 8  │
│  Display-only, shown in column order. Roles count toward the 8.             │
│                                                                             │
│  ☑  1 · Posisi yang dilamar         Short label [ Position applied____ ]    │
│  ☐  3 · Pendidikan terakhir                                                 │
│  ☑  5 · Pengalaman kerja            Short label [ Experience___________ ]    │
│  ☐  6 · Gaji yang diharapkan                                                │
│  ☐  8 · Bersedia ditempatkan di luar kota?                                  │
│                                                                             │
│  Timestamp (column 0) is imported automatically and never picked.           │
│                                                                             │
│  Name and Contact email are required.            [ Cancel ]  [ Save layout ]│
└─────────────────────────────────────────────────────────────────────────────┘
```

Behavior notes:

- **Role selects** list every column as `ordinal · header` (header truncated to ~48 chars,
  full text in the caption under a bound row). A column bound to one role is disabled in
  the other three selects. "Not assigned" is the empty option. **No auto-detection**, per
  ADR-0013: the panel never pre-fills a role, even on first open.
- **Label inputs** appear only on picked rows (bound roles and checked display columns).
  Placeholder is the truncated header; blank clears back to the header. Trimmed, single
  line, ≤40 chars with a live character count. The original header stays visible beside
  every label, per the issue's verifiability rule.
- **Picked counter** = bound roles + checked display columns, live. Checking a 9th column
  is blocked at the control with the counter turning into the explanation
  ("8 columns at most, roles included").
- **Display checkbox list excludes role-bound columns** — a column is either a role or a
  display answer, never both. Un-binding a role moves the column back into this list,
  unchecked, and discards its label.
- **Validation** via `<UForm :schema>` with a Zod schema in `validation.ts`: Name and
  Contact email bound, ≤8 picked, label rules. Errors render inline on submit; the footer
  sentence ("Name and Contact email are required.") is replaced by the first error.
- **Save** on an existing layout re-projects over stored rows; the success toast says so
  ("Layout saved. Candidate details re-filled from the form answers where HR hasn't typed
  overrides.") so the back-fill is not a silent surprise.

## Guided mode (first upload, blocking)

Same panel, three deltas:

```
┌ Set up the form layout to finish importing ─────────────────────────────────┐
│ ┌─────────────────────────────────────────────────────────────────────────┐ │
│ │ Import of "Q4 Operator Form (Responses).csv" is waiting. Bind Name and  │ │
│ │ Contact email, then save to import 312 rows.                            │ │
│ └─────────────────────────────────────────────────────────────────────────┘ │
│  ... identical roles + display zones ...                                    │
│                                  [ Cancel import ]  [ Save layout & import ]│
└─────────────────────────────────────────────────────────────────────────────┘
```

- `prevent-close` modal: no backdrop dismiss, no `Esc`, no `×`. The only exits are
  "Cancel import" (drops the held file, nothing is saved) and the primary action.
- Columns come from the held file's header row (see open question 1).
- Primary button runs save + import as one flow; while it runs, the button shows
  "Saving and importing…" and everything else is disabled.
- On success the panel closes into the normal import result (existing toast + summary
  line from `useFormResponseImport`).

## Drift dialog (re-upload with changed mapped headers)

Small dialog, not the panel. It lists only the changed ordinals.

```
┌ Form headers changed ───────────────────────────────────────────────────────┐
│ The file's headers differ from the saved layout at 2 columns. Confirm the   │
│ mapping still points at the right columns, or re-map first.                 │
│                                                                             │
│  2 · bound to Name · label "Candidate name"                                 │
│     Was:  Nama Lengkap (Full name)                                          │
│     Now:  Nama lengkap sesuai KTP                                           │
│                                                                             │
│  5 · shown as form answer                                                   │
│     Was:  Pengalaman kerja                                                  │
│     Now:  Pengalaman Kerja (tahun)                                          │
│                                                                             │
│ Confirming keeps the mapping and labels and continues the import.           │
│                                                          [ Cancel import ]  │
│                        [ Re-map… ]              [ Confirm mapping & import ]│
└─────────────────────────────────────────────────────────────────────────────┘
```

- Old and new headers render verbatim (the old one is the snapshot); the label sits
  beside its column so HR re-verifies it at the same glance.
- Neutral styling (info accent), not amber and not red: drift is an expected pause, not
  a failure and not a lifecycle conflict.
- "Confirm" re-sends the held file with a `confirmDrift` flag; "Re-map…" swaps this
  dialog for the full panel pre-filled with the current mapping, and saving completes the
  import. "Cancel import" drops the file. Already-imported candidates are untouched in
  all three paths.

## Read-only summary + empty state (vacancy settings area)

A `UCard` titled "Form layout" on the vacancy detail page, beside the round management
chrome:

```
┌ Form layout ────────────────────────────────┐
│ Name           2 · Nama Lengkap             │
│ Contact email  4 · Email aktif              │
│ Contact phone  Not assigned                 │
│ CV link        7 · Link CV (Google Drive)   │
│ Form answers   4 columns shown              │
│                              [ Edit layout ]│
└─────────────────────────────────────────────┘
```

Empty state (no valid layout yet), one sentence plus one action, per ADR-0008 state
language:

```
┌ Form layout ────────────────────────────────┐
│ No form layout yet. The first .csv upload   │
│ walks you through mapping its columns.      │
│                              [ Set up now ] │
└─────────────────────────────────────────────┘
```

("Set up now" opens the panel in edit mode but with no headers available, so v1 may
disable it and keep the sentence only — decide with the backend contract; a layout saved
without a header snapshot is only useful if the server can supply headers.)

## Slice map (client)

Per the vue-feature-slices rules, one feature module, wired into the existing
vacancy-detail page:

```
src/features/form-layout/
  api.ts                     FormLayoutDto, ColumnMapping; getFormLayout, saveFormLayout,
                             importWithLayout (guided complete), confirmDrift
  useFormLayout.ts           load/save lifecycle + view-state union
                             ('loading | error | empty | ready'), guided/drift state
  validation.ts              Zod schema: name+email bound, ≤8 picked, label ≤40 chars
  format.ts                  "ordinal · header" labels, header truncation, picked counter
  components/
    FormLayoutDialog.vue     the panel; props mode: 'edit' | 'guided', heldFile
    RoleBindingFields.vue    the four role selects + label inputs
    DisplayColumnList.vue    checkbox list + label inputs + live counter
    DriftDialog.vue          changed-ordinal list, confirm / re-map / cancel
    FormLayoutSummary.vue    read-only card + empty state
```

Page wiring in `pages/vacancy-detail/VacancyDetailView.vue`: "Form layout" header button,
plus the import-form path (`useFormResponseImport`) intercepting 409s to open guided or
drift mode with the held file.

Seam tests (after implementation, per ADR-0003): mount `VacancyDetailView`, stub `fetch`,
assert the issue's seam-test list as user-visible outcomes (panel opens, role binding,
counter, guided blocking, drift list, confirm/re-map flows).

## Open seam questions for the backend slice

1. **Guided-mode headers.** The panel needs the held file's header row before any layout
   exists. Cheapest seam: the client parses the first CSV line locally (the file is
   already in the browser) and the save endpoint accepts the header snapshot alongside
   the mapping. Alternative: a `POST .../form-layout/headers` endpoint that takes the
   file and returns parsed headers. Local parse keeps the server surface smaller; the
   snapshot stays server-owned on save.
2. **Refusal shape.** Suggest `409` with a problem-details extension:
   `kind: 'no-layout' | 'header-drift'`, plus the drift payload (changed ordinals, old
   and new headers) so the dialog renders without a second call.
3. **Re-projection feedback.** When an edit-mode save re-projects stored rows, return
   counts (`candidatesUpdated`, `typedOverridesKept`) so the toast can say what happened.

## States covered (ADR-0008 #14)

- Loading: shape-matched skeleton of the two zones (select rows + checkbox rows).
- Error: inline alert in the panel's region; the panel itself failing to load renders the
  error in the summary card with a Retry button. Never toast-only.
- Empty: the sentence-plus-action card above.
- Every async control has a busy label ("Saving and importing…") and disables re-entry.
