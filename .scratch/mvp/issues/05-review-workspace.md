# 05: Review workspace

**What to build:** The review screen (S4): side-by-side workspace for one candidate at a
time. Left: vacancy skills requirements panel (manual checklist), the candidate's details
and source sender, the original email subject/body (expandable), and a notes editor with
save. Right: multi-page PDF viewer with pagination. Bottom action bar: Prev / Shortlist /
Flag / Reject / Next, with a position indicator (e.g. 1/30). Shortlist/Flag/Reject set the
candidate's review status; vacancy progress numerator advances.

**Blocked by:** 04-candidate-list

**Status:** implemented

- [x] PDF viewer renders multi-page CVs with pagination controls
- [x] Vacancy skills requirements and candidate details shown beside the PDF
- [x] Original email subject and body viewable
- [x] Notes editable and saved per candidate
- [x] Shortlist / Flag / Reject set review status; Prev / Next navigate candidates; position indicator accurate
- [x] Vacancy progress reflects reviewed candidates
- [x] Backend and frontend tests pass (written after implementation)

**Amendment (2026-09-03, V1 re-slice):** V1 has no PDF extraction — "candidate extracted
data" becomes manually entered Candidate Details (ticket 08), and requirements stay a
manual checklist. Computed match arrives in V3 (ADR-0009).

**Amendment (2026-09-06, post-implementation alignment):** the shipped workspace grew
beyond the bullets above; this amendment records it as spec rather than scope creep.

- **Keyboard triage.** The workspace is keyboard-first (US-17's "fast repeated rhythm"):
  S/F/R set Shortlist/Flag/Reject, ←/→ move between candidates, digits toggle
  requirement checks, E/O/N focus the details/email/notes panels, ? opens a shortcuts
  help modal, Esc exits. Two states, now in CONTEXT.md: **Triage Mode** (no editable
  field focused, shortcuts armed) and **Editing Mode** (a text field has focus; decision
  shortcuts stay inert until Esc or N). The bottom action bar stays — every shortcut has
  a visible button; keys never replace the bar.
- **Saved-details gate.** Shortlist is blocked — with a toast telling HR why — until the
  candidate's name and contact email are saved (ticket 08). The V1 re-slice made those
  details the *only* candidate identity (no extraction), so shortlisting an anonymous
  record would poison the shortlist group that ticket 06 emails. Flag and Reject stay
  ungated. (Shipped in d52a2dc; recorded here after the fact.)
- **Position indicator.** Renders as space-padded tabular numerals ("1 / 30") for
  stable width while paging; the vacancy progress count is a separate label.
