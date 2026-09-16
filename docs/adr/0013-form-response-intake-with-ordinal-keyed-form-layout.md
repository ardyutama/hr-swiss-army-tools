# ADR-0013: Form-response intake with ordinal-keyed per-vacancy form layout

## Status

Accepted

## Context

The V1 intake is `.eml` email files only (see ADR-0002, the `ImportCandidatesCommandHandler`
seam, and `EmlParser`). Real hiring volume at the factory arrives as **Google Forms CSV
exports** — one form per vacancy, ~300 responses in the first 30 minutes, columns
localized in Indonesian with multi-line headers, and a healthy dose of human noise
(duplicates, wrong-field pastes, empty emails). HR must consciously export the CSV and
upload it per vacancy — no sync, no polling.

Email-shaped assumptions fail here: there is no "sender" and no attachment; the CV is a
Google Drive *link*; and the meaningful fields (position, willingness, education,
experience, salary, CV) live in columns whose header text is long, localized, and edited
by HR mid-vacancy.

## Decision

1. **New intake source, not a replacement.** `Form Response` sits beside `Source Email`
   under the new umbrella **Intake Source** ([CONTEXT.md](../../CONTEXT.md)). One intake
   round may mix both. The review workspace renders whichever evidence exists; email-only
   panels collapse for form candidates and vice-versa.
2. **Raw row is preserved verbatim.** Every CSV row's cells are stored untouched on the
   candidate (raw form response). Screening and layout are *read-time projections* over
   stored raw data, never parse-once-and-discard.
3. **Form Layout is vacancy-owned, ordinal-keyed.** Columns are identified by **ordinal
   position**, never by header text. A header snapshot is kept only for display and for
   **Header Drift** detection: when a re-upload's header at a mapped ordinal differs, the
   import pauses behind a drift dialog (old → new) and HR confirms or re-maps before
   proceeding. HR edits to the Google Form therefore never silently re-point a mapping.
4. **Four special roles, everything else display-only.** Name, Contact Email, Contact
   Phone, CV Link each bind to at most one column and *pre-fill* the editable Candidate
   Details at import (HR can still correct them — a form field is a convenience, not
   truth). All other picked columns are display-only form answers, shown in column order,
   capped at 12 per vacancy.
5. **Identity and duplicates.** A response's identity key is the normalized email, falling
   back to digits-only phone, falling back to no key (always a new candidate). Within one
   vacancy, the same email key keeps only the latest response (form Timestamp wins) as its
   Form Response; earlier ones are retained as hidden prior submissions. A re-upload of a
   fresher export into the active round is **additive**: new keys become candidates, known
   keys update the stored Form Response and raise the **Resubmitted** indicator, and review
   status / notes / requirement reviews / typed details are never altered by a re-upload.
6. **CV is a link, not a fetch.** The CV Link column is stored as a URL. The review page
   exposes it as a header button and keyboard shortcut **C** that opens the link in a new
   tab. We deliberately do **not** fetch or OCR Drive files (permission-gated links fail
   silently; ADR-0009's OCR pipeline stays email-only for V1).

## Considered options

- **(a) Header-text mapping** — rejected: HR edits question wording mid-vacancy; every
  mapping would silently break.
- **(b) One shared form for many vacancies** (route rows by the position column) —
  rejected for V1: the real workflow is one form per vacancy; the 5-position CSV in
  evidence is an anomaly, not the norm.
- **(c) Server-side sync / Sheets API / OAuth** — rejected: this is an on-prem Windows
  desktop tool (see the 2026-09-15 deployment findings); HR consciously imports.

## Consequences

- The `Candidate` aggregate gains an Intake Source discriminator and a raw Form Response
  payload; the EML path is untouched.
- A new `Form Layout` configuration entity hangs off the vacancy (ordinal-keyed mapping +
  header snapshot + the four role bindings).
- The review workspace gains a form-evidence variant (see the ADR-0008 amendment):
  Form Answers dominant, CV-link button in the header.
- `Prior Application Notice` now matches on identifying email across the whole vacancy,
  any round including the current one, and across intake sources.
- Future work the glossary now permits: per-form screening rules (ADR-0014), and —
  much later — a Sheets API importer that reuses the same Form Response storage.
