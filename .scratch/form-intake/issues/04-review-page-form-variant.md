# 04: Review workspace — form-evidence variant

**Blocked by:** 02-form-layout-config

**Status:** ready-for-agent

**Terms:** Form Layout, Resubmitted — [CONTEXT.md](../../../CONTEXT.md).
**Decision:** ADR-0008 amendment (evidence-dominant review variant),
[ADR-0013](../../../docs/adr/0013-form-response-intake-with-ordinal-keyed-form-layout.md).

## What to build

The review workspace renders by **Intake Source**. For a **form-sourced** candidate there
is no local PDF, so the dominant area shows the form evidence instead of the CV viewer.

- **Form Answers panel** takes the ~55–60% dominant area: the vacancy's picked display
  columns (Form Layout), in **column order**, rendered read-only from the stored raw
  Form Response.
- **CV link:** the bound CV Link column becomes a header button **plus keyboard shortcut
  `C`** that opens the Google Drive URL in a new tab. **No iframe** (permission-gated
  Drive links fail silently in an embed). If the link cell is empty, the button renders
  disabled with a "No CV link" label — never hidden, so HR sees the gap.
- **Resubmitted flag:** if the stored Form Response was refreshed by a later upload,
  surface the indicator on the review page.
- **Data column unchanged:** requirements, editable Candidate Details (pre-filled by the
  layout's roles), notes, and the action bar (S/F/R, ←/→) behave exactly as today.
- **Email candidates** keep the existing PDF-dominant layout untouched.
- Keep ADR-0008's density, hit-target, and keyboard-hint grammar; the Form Answers panel
  uses the same shape-matched skeleton and inline-error state language as the CV pane.

## Out of scope

The candidate-list table (it stays name/email/phone/status — the picked columns live only
on the review page); screening chips on the list (issue 03).

## Seam tests

- Form candidate → Form Answers panel renders the picked columns in column order.
- `C` / button opens the stored CV link in a new tab; empty link → disabled + "No CV link".
- Resubmitted indicator appears when the stored response was refreshed.
- Email candidate → PDF-dominant layout unchanged (regression).
- Decision shortcuts (S/F/R) and auto-save notes behave identically in both variants.

## Acceptance

- [ ] Review workspace branches by Intake Source
- [ ] Form Answers panel (picked columns, column order, read-only)
- [ ] CV-link header button + `C` shortcut, new tab, empty-link disabled state
- [ ] Resubmitted indicator
- [ ] Data column / action bar unchanged
- [ ] Backend + frontend seam tests pass (written after implementation)
