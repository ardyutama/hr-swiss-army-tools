# 02: Per-vacancy Form Layout (column mapping)

**Blocked by:** 01-csv-form-response-import

**Status:** in-progress — design settled by grill session (2026-09-18); implementation not started. Existing code: backend `FormLayouts` slice is roles-only (Get/Upsert, `FormLayout` with 4 ordinal scalars + `HeaderSnapshot`); import gate exists in `ImportFormCommandHandler` (409 `Candidates.FormLayoutRequired` before parsing); no client form-layout feature. UI shape pinned by `02-form-layout-config.layout.md` (Nuxt UI v4, ADR-0008 language; design-taste-frontend scoped out as landing-page rules).

**Terms:** Form Layout, Header Drift, Column Label — [CONTEXT.md](../../../CONTEXT.md).
**Decision:** [ADR-0013](../../../docs/adr/0013-form-response-intake-with-ordinal-keyed-form-layout.md).

## What to build

A vacancy-owned **Form Layout**: the ordinal-keyed mapping of the form's CSV columns onto
the review workspace, edited from a **vacancy settings panel** ("Form Layout" section).

- Columns are identified by **ordinal position**, never header text. Store a read-only
  **header snapshot** for display and drift detection.
- **Four special roles**, each bound to at most one column: **Name, Contact Email,
  Contact Phone, CV Link**. Roles are **always bound manually by HR, never auto-detected**.
  Bound roles *pre-fill* the editable Candidate Details at import (HR can correct them
  afterward — a form field is a convenience, not truth).
- **Display fields:** any other picked columns are display-only form answers on the review
  page, shown in **column order**; a vacancy picks at most **8 columns in total, roles
  included**.
- The form **Timestamp is never a picked column** — it is system data read from ordinal 0
  (Google Forms convention) and stored on the Form Response at import (issue 01); the
  review page surfaces it as system data (issue 04).
- **Column Labels:** each picked column (roles included) may carry HR's display-only short
  name, bound to the ordinal — trimmed, single line, ≤40 chars, blank clears back to the
  header. The original header snapshot is always shown beside it (layout editor, drift
  dialog), so the label stays verifiable without an edit log. A drift confirmation
  re-verifies the label; un-picking the column discards it. Labels never identify columns,
  never affect drift detection, and never Settle (layout configuration, not review data).
- Validation: a layout is invalid without at least **Name + Contact Email** assigned.
- Editing the layout re-projects over stored raw rows — no re-import needed. Role-bound
  columns pre-fill Candidate Details **except where HR already typed values** (typed
  edits always win).
- **Import is hard-gated on a valid layout.** A vacancy with no valid layout cannot
  import form responses: the first CSV upload routes into the guided panel, the import
  completes there once Name + Email are bound, and direct import attempts are refused
  until a valid layout exists. Every imported row is therefore already projected through
  the roles.
- **Header Drift gate:** on a re-upload whose header at a mapped ordinal differs from the
  snapshot, pause the import behind a drift dialog listing every changed ordinal
  (old → new). HR confirms the mapping still holds or re-maps, then import proceeds.
  Already-imported candidates keep their stored Form Responses untouched.
- First CSV upload on a vacancy with no layout opens the same panel in a **guided mode
  (not a separate wizard) that is blocking**: the uploaded file is held while HR binds
  Name + Email (plus any optional roles / display fields), the header snapshot is taken
  from that file, and saving completes the import in the same flow.

## Grill decisions (2026-09-18, confirmed by product owner — all recommended options accepted)

Domain model:
1. Unify picked columns into one `FormLayoutColumn` collection (`Ordinal`, `Role?`, `Label?`),
   stored jsonb on the single per-vacancy layout row (mirrors `CandidateFormResponse.Cells`
   precedent). Roles become computed projections; all invariants (≤8 picked total, Name+Email
   bound, ordinal uniqueness, role/display exclusivity, in-range) live in one `Validate`.
   Migration replaces the four ordinal columns + old snapshot shape; dev data sacrifice OK.
2. Candidate Detail provenance: per-field enum `None | FormPrefilled | Typed` on
   `FullName`/`ContactEmail`/`ContactPhone`. Import/re-projection writes only non-`Typed`
   fields; `UpdateDetails` marks `Typed`. Rejected value-compare (silently wrong).
3. Re-projection (layout save) applies to the **active round's** candidates only — never
   crosses a settled round (CONTEXT.md Form Layout entry amended).
4. `FormLayout.DetectDrift(newHeaders)` and the confirm-time snapshot adoption are domain
   methods; `Candidate.PrefillDetailsFromLayout(layout)` reads its own current Form Response
   cells. Handlers iterate candidates themselves (existing handler convention).
5. Typed **Contact Phone** joins `UpdateDetails` in this issue (command → domain → validator
   → response → existing details form input); review-page variant stays with issue 04.
6. On **resubmit**, `FormPrefilled` fields re-project from the new current row; `Typed`
   fields never move.
7. Pre-fill is **best-effort per field**: trim cell; a value violating `CandidateDetailsRules`
   is left empty and the row still imports (never a row-killer). Provenance stamps only on
   actual writes. Typed path stays strict.
8. Terms captured in CONTEXT.md: **Picked Column**, **Form Answer**; Form Layout + Header
   Drift entries amended (settled-round clause, confirm-adopts-snapshot).

API seam:
9. `Error` gains an optional extensions dictionary flowed into RFC 7807 extension members by
   `CustomResults.Problem` (mirrors `ValidationError.Errors`). Two refusal codes, no `kind`
   field: `Candidates.FormLayoutRequired` (carries `headers`) and `Candidates.FormHeaderDrift`
   (carries `headers` + `changes: [{ordinal, was, now}]`).
10. Handler order flipped to **parse-then-gate**: headers come from `FormCsvParser`
    (captures header row into `ParsedFormCsv`); the client never parses CSV (no client CSV
    dependency; server handles quoted multi-line localized headers).
11. Guided-mode (first upload) and drift re-map are one atomic call: `ImportFormCommand`
    gains an optional layout payload + `confirmDrift` flag (mutually exclusive); layout
    payload upserts the layout in the same transaction, stamps the snapshot from that file,
    and skips the drift check (it *is* the re-map). Stateless — the client re-sends the
    held file.
12. **Header snapshot is purely server-stamped**: the `Upsert` command drops client-sent
    `HeaderSnapshot` (breaking change to the young command); snapshots are written only by
    import flows carrying a real file. Edit-mode saves never touch it.
13. Drift confirm **adopts the new snapshot** (CONTEXT.md amended); otherwise the pause
    would re-trigger forever.
14. Column-count edge: out-of-range picked ordinal appears in the drift list as
    "column no longer present"; **Confirm blocked** while any picked ordinal is out of range
    (Re-map is the only resolution). Extra columns never count as drift — comparison at
    picked ordinals only.
15. `Upsert` response gains `candidatesUpdated` / `typedOverridesKept` so the save toast can
    state the back-fill; import response contract unchanged.
16. Validation split: FluentValidation holds format rules (label trimmed/single-line/≤40,
    ordinals non-negative); domain holds structural invariants.

Client:
17. One `src/features/form-layout/` module per the layout doc's slice map; panel is a
    `UModal` on `VacancyDetailView`, **no new route**. Empty state ships sentence-only in v1
    (a layout is invalid without a snapshot, and no snapshot exists before the first file).
18. Refusal ownership: `useFormResponseImport` gains `pendingRefusal` state
    (`{ file, headers, changes? }`) set on the two refusal codes instead of alerting; the
    held file lives in the composable (single owner, cleared by the existing vacancy/round
    watcher); page watches it to open guided/drift. New `importWithLayout(mapping)` on the
    same composable → same endpoint, layout serialized as one JSON multipart field
    (server deserializes it); drift confirm re-sends with `confirmDrift`. All
    import-endpoint calls stay in `import-form/api.ts`; `form-layout/api.ts` keeps GET/PUT.
19. `problem-details.ts` must not route the two refusal codes through the generic 409 path;
    drift UI is neutral (not amber — amber stays reserved for Flagged / Lifecycle Conflict).

## Out of scope

Screening rules over mapped columns (issue 03), the review-page Form Answers rendering
(04).

## Seam tests

- Save a layout (roles + display fields); invalid without Name + Contact Email.
- Save a label on a picked column; blank clears back to header; label survives a drift
  confirmation; un-picking discards it.
- Import pre-fills Candidate Details from bound roles; typed edits survive a re-upload.
- Import on a vacancy with no layout → refused; first upload routes into guided mode and
  completes the import once Name + Email are bound.
- Layout saved after rows exist → re-projection back-fills Candidate Details from roles,
  except where HR already typed values.
- Re-upload with a changed mapped header → drift dialog (label shown beside old → new
  header) → import blocked until confirmed / re-mapped.
- Re-upload with unchanged headers → no drift, imports cleanly.
- >8 picked columns in total (roles included) → validation error.

## Acceptance

- [ ] Vacancy settings "Form Layout" panel with role dropdowns + display-field checkboxes
- [ ] Label input per picked column, original header always shown beside it
- [ ] Ordinal-keyed mapping + header snapshot + labels persisted per vacancy
- [ ] Roles bound manually by HR (no auto-detection); roles pre-fill Candidate Details
- [ ] Import hard-gated on a valid layout; first upload routes into blocking guided mode
- [ ] Layout edits re-project over stored rows; typed Candidate Details survive
- [ ] Drift dialog on changed mapped headers; import pauses until resolved
- [ ] ≤8 picked columns in total (roles included), Name + Email required
- [ ] Typed Contact Phone supported in Candidate Details (existing UpdateDetails seam)
- [ ] Per-field provenance (`None | FormPrefilled | Typed`) governs pre-fill and re-projection
- [ ] Backend + frontend seam tests pass (written after implementation)
