# 04: Candidate list per vacancy

**What to build:** The candidate list screen (S3): from a vacancy, HR sees one row per
candidate with display name, notes summary, review status, and Send/Delete actions (Send
may be a placeholder until templates exist). Display name falls back to sender name, then
sender email, then email subject until HR types a real name (ticket 08). Rows navigate
into the review workspace. The header shows the vacancy title with date and a "Send Email
to all candidate" button (disabled until ticket 06).

**Blocked by:** 03-eml-import

**Status:** in-progress

- [x] List candidates of a vacancy: display name (sender fallback), notes, review status
- [x] Delete a candidate
- [x] Review-status filter chips (New/Flagged/Shortlisted/Rejected/All) with live counts;
      active chip highlighted, chips uniform otherwise; combined with search = AND semantics
- [x] Client-side search input (display name / sender email / subject) in one toolbar row
      above the table: chips left, search right
- [x] Shared "No candidates match" empty state with Clear filters when filter/search
      yields zero (distinct from the true no-candidates empty state, which stays the
      Import .eml prompt)
- [x] "Received" date column (source sent-at), oldest-first default; clickable header
      toggles oldest/newest (only sortable column)
- [x] CV presence column: paperclip when a CV document exists, "No CV" warning badge when
      the source email had no PDF
- [x] "Status" column renamed "Review status"
- [x] Column order: Candidate | Received | CV | Notes | Review status | Actions
- [x] Whole-row click opens the review workspace (chevron affordance, action buttons stop
      propagation) — route wired when ticket 05-review-workspace lands
- [x] Header shows vacancy title with date and a (disabled) "Send Email to all candidate" button
- [x] Backend and frontend tests pass (written after implementation)

**Amendment (2026-08-31):** the S2 drop zone moved into an "Import .eml" dialog on the
candidate list page (user request) instead of an inline-only empty state; the empty state
now offers a button that opens the same dialog.

**Amendment (2026-09-03, V1 re-slice):** the match column is removed — V1 has no
extraction (match returns in V3, ADR-0009). Display name is a sender-fallback display rule
only; the stored name stays empty until typed.

**Amendment (2026-09-03, page-improvement grilling, round 1 settled):** V1 scope grows:
review-status filter chips with live counts; client-side name/sender/subject search;
"Received" (source sent-at) date column, oldest-first default with toggle; whole-row click
opens the review workspace (chevron affordance, wired when ticket 05 lands); CV presence
indicator (paperclip / "No CV" badge); "Status" column renamed "Review status". **Batch
selection (multi-row checkbox actions) explicitly deferred** — revisit only if HR deletes
in batches in practice. Duplicate submissions stay silent: when one person sends more than
one source email to the same vacancy, each email imports as its own candidate — no detect,
warn, or merge — and HR resolves duplicates through review and Candidate Removal.
