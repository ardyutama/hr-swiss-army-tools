# Spec: Form-intake (Google Forms CSV)

**Source of truth for terms:** [CONTEXT.md](../../CONTEXT.md) — *Intake Source, Form
Response, Form Layout, Header Drift, Screening Rule, Screened Out, Resubmitted*.

**Decisions:** [ADR-0013](../../docs/adr/0013-form-response-intake-with-ordinal-keyed-form-layout.md)
(form intake + ordinal-keyed form layout),
[ADR-0014](../../docs/adr/0014-screening-rules-as-computed-disposition-never-deletion.md)
(screening as computed disposition), ADR-0008 amendment (evidence-dominant review variant).

## What this feature is

HR recruits factory operators through a **Google Form per vacancy** and exports the
responses as CSV. Today the app only imports `.eml` emails. This feature adds the CSV as a
second **Intake Source** beside the email, so a vacancy's intake round can receive both.

The shape, settled in a 3-round grill session (2026-09-16):

- **Manual, conscious import.** HR exports the CSV from Google Forms and uploads it into
  the active round. No sync, no polling, no OAuth. One form : one vacancy.
- **Raw rows preserved.** Every CSV row's cells are stored verbatim on the candidate.
  Layout and screening are read-time projections over that stored raw data.
- **Ordinal-keyed Form Layout.** The vacancy maps CSV columns by **ordinal position**
  (never header text). Four special roles — Name, Contact Email, Contact Phone, CV Link —
  pre-fill editable Candidate Details; up to 12 more picked columns are display-only form
  answers on the review page, shown in column order.
- **Header Drift gate.** A re-upload whose headers differ at a mapped ordinal pauses
  behind a drift dialog (old → new) until HR confirms or re-maps.
- **Dedupe & Resubmitted.** Identity = normalized email → digits-only phone → no key.
  Same email key keeps the latest response (form Timestamp wins); a re-upload is additive,
  updates known keys' stored Form Response, raises **Resubmitted**, and never touches
  review status, notes, requirement reviews, or typed details.
- **CV is a link.** The CV Link column is a Google Drive URL opened via a header button /
  **C** shortcut. No fetch, no OCR.
- **Screening, never deletion.** Vacancy-owned Screening Rules (≤5, AND, text operators)
  compute a **Screened Out** disposition over stored raw responses. Screened-out
  candidates are imported, stored, hidden from the default list behind a count badge and
  toggle, never deletable, and reclassifiable by editing rules. Settles at round closure.
  Email candidates are never screened.

## Non-goals (V1)

- One shared form feeding many vacancies (the 5-position CSV is an anomaly).
- Server-side sync / Google Sheets API / OAuth.
- Fetching or OCR-ing Drive CVs.
- Type-aware screening (number ranges, yes/no normalization).
- Client-side per-user layout preferences (layout is the vacancy's shared config).

## Slices

In [workflow](../../docs/agents/workflow.md) order — each is a full vertical slice
(schema → API → client → seam tests). One issue file per slice under `issues/`:

1. `01-csv-form-response-import` — parse + store raw form responses, dedupe, Resubmitted.
2. `02-form-layout-config` — ordinal-keyed mapping, special roles, header drift gate.
3. `03-screening-rules` — rule editor, live counts, computed Screened Out disposition,
   list toggle + rule chips.
4. `04-review-page-form-variant` — Form Answers panel, CV-link button + **C** shortcut.

## Sequencing

01 and 02 are the foundation (02 depends on 01's stored raw rows). 03 depends on 02's
column mapping (rules are over mapped columns). 04 depends on 02 (it renders the picked
columns) and is unblocked by 03. Recommended order: 01 → 02 → (03 ∥ 04).

## Performance targets

Import 5,000 rows < 10 s. Candidate list paginates at 100/page, < 300 ms — screening
exclusion is server-side because the list can no longer assume "fetch everything".
