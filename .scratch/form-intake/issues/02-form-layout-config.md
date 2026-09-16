# 02: Per-vacancy Form Layout (column mapping)

**Blocked by:** 01-csv-form-response-import

**Status:** ready-for-agent

**Terms:** Form Layout, Header Drift, Column Label — [CONTEXT.md](../../../CONTEXT.md).
**Decision:** [ADR-0013](../../../docs/adr/0013-form-response-intake-with-ordinal-keyed-form-layout.md).

## What to build

A vacancy-owned **Form Layout**: the ordinal-keyed mapping of the form's CSV columns onto
the review workspace, edited from a **vacancy settings panel** ("Form Layout" section).

- Columns are identified by **ordinal position**, never header text. Store a read-only
  **header snapshot** for display and drift detection.
- **Four special roles**, each bound to at most one column: **Name, Contact Email,
  Contact Phone, CV Link**. Bound roles *pre-fill* the editable Candidate Details at
  import (HR can correct them afterward — a form field is a convenience, not truth).
- **Display fields:** any other picked columns are display-only form answers on the review
  page, shown in **column order**, capped at **12** per vacancy.
- **Column Labels:** each picked column (roles included) may carry HR's display-only short
  name, bound to the ordinal — trimmed, single line, ≤40 chars, blank clears back to the
  header. The original header snapshot is always shown beside it (layout editor, drift
  dialog), so the label stays verifiable without an edit log. A drift confirmation
  re-verifies the label; un-picking the column discards it. Labels never identify columns,
  never affect drift detection, and never Settle (layout configuration, not review data).
- Validation: a layout is invalid without at least **Name + Contact Email** assigned.
- Editing the layout re-projects over stored raw rows — no re-import needed.
- **Header Drift gate:** on a re-upload whose header at a mapped ordinal differs from the
  snapshot, pause the import behind a drift dialog listing every changed ordinal
  (old → new). HR confirms the mapping still holds or re-maps, then import proceeds.
  Already-imported candidates keep their stored Form Responses untouched.
- First CSV upload on a vacancy with no layout opens the same panel in a guided mode (not
  a separate wizard).

## Out of scope

Screening rules over mapped columns (issue 03), the review-page Form Answers rendering
(04).

## Seam tests

- Save a layout (roles + display fields); invalid without Name + Contact Email.
- Save a label on a picked column; blank clears back to header; label survives a drift
  confirmation; un-picking discards it.
- Import pre-fills Candidate Details from bound roles; typed edits survive a re-upload.
- Re-upload with a changed mapped header → drift dialog (label shown beside old → new
  header) → import blocked until confirmed / re-mapped.
- Re-upload with unchanged headers → no drift, imports cleanly.
- >12 display fields → validation error.

## Acceptance

- [ ] Vacancy settings "Form Layout" panel with role dropdowns + display-field checkboxes
- [ ] Label input per picked column, original header always shown beside it
- [ ] Ordinal-keyed mapping + header snapshot + labels persisted per vacancy
- [ ] Roles pre-fill Candidate Details at import
- [ ] Drift dialog on changed mapped headers; import pauses until resolved
- [ ] ≤12 display fields, Name + Email required
- [ ] Backend + frontend seam tests pass (written after implementation)
