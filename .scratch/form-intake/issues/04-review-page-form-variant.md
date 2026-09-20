# 04: Review workspace — form-evidence variant + list intake-source identification

**Blocked by:** 02-form-layout-config

**Status:** ready-for-agent; design settled 2026-09-20 (grill session — see Grill decisions);
companion layout doc: [04-review-page-form-variant.layout.md](04-review-page-form-variant.layout.md)

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
- **Form timestamp as system data:** the stored form Timestamp (issue 01's system data,
  never a picked column) is surfaced on the review page — it does not appear in the
  Form Answers panel.
- **Data column unchanged:** requirements, editable Candidate Details (pre-filled by the
  layout's roles), notes, and the action bar (S/F/R, ←/→) behave exactly as today.
- **Email candidates** keep the existing PDF-dominant layout untouched.
- Keep ADR-0008's density, hit-target, and keyboard-hint grammar; the Form Answers panel
  uses the same shape-matched skeleton and inline-error state language as the CV pane.

## Out of scope

~~The candidate-list table (it stays Candidate | Received | CV | Notes | Review status |
Actions)~~ — **superseded by grill decision 1 (2026-09-20)**: the list keeps those six
columns but differentiates intake source within them (no new column). Still out of scope:
screening chips on the list (issue 03).

## Grill decisions (2026-09-20)

Second pass on this issue: differentiate who was ingested from a Form Response (CSV) vs a
Source Email (.eml) on the candidate list, and make the review variant conditional on
which evidence exists. Numbering starts at 1 for this issue's first grill.

1. **List CV cell becomes a four-state matrix.** Email + PDF(s) → paperclip icon
   (unchanged). Email + no PDF → amber "No CV" badge (unchanged; genuine gap). Form +
   link present → neutral link icon + "Link" label (evidence exists, just external —
   never the false-alarm amber). Form + empty link cell → amber "No CV link" badge
   (genuine gap, distinct copy from email's "No CV").
2. **The form "Link" cell is clickable.** It opens the stored Drive URL in a new tab,
   `@click.stop` so the row on the review page header
- [ ] Source pill + form timestamp in the review header (both variants)
- [ ] Data column / action bar unchanged
- [ ] List CV cell: four-state matrix (paperclip / "No CV" / clickable "Link" / "No CV link")
- [ ] List source icon before the candidate name, tooltip names the source
- [ ] Received shows the form Timestamp for form candidates; list sort coalesces iting per-row `@click.stop` actions (Send email,
   Delete) are the precedent.
3. **Muted source icon before the candidate name** (tooltip names the source):
   `i-lucide-mail` for Source Email, `i-lucide-file-text` for Form Response. No new
   column — the table stays at six columns, density 5–6.
4. **Received shows the form Timestamp for form candidates** (it is "—" today even
   though issue 01 stores the timestamp as system data). Backend rider: the list
   reader's sort key coalesces `source_sent_at` with the current Form Response's parsed
   timestamp so ordering matches what HR sees, instead of sinking all form rows below
   dated email rows.
5. **Review page = issue 04's spec plus a source-identity header on both variants.** The
   header meta line carries a source pill ("Form Response" / "Source Email"), the form
   Timestamp (form only — system data, not a picked column), and the Resubmitted badge
   when true. No second visual language: same panels, same grammar; the header tells HR
   which evidence they are looking at.
6. **Email candidate with zero PDFs keeps the PDF pane** with its existing empty state.
   A missing attachment is a gap, not a different evidence type — no third variant.
7. **Bookkeeping**: this issue is amended in place (this section + the struck out-of-scope
   note + the companion layout doc); no new issue file.
8. **List DTO shape**: `CandidateSummaryResponse` gains one nullable `cvLink` field
   (full URL), resolved by the list reader via the vacancy's Form Layout CV Link ordinal
   → the current Form Response's cell. The frontend derives the four display states from
   it; no separate boolean. Implementation detail, recorded for the contract.

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
