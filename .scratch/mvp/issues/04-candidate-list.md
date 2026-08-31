# 04: Candidate list per vacancy

**What to build:** The candidate list screen (S3): from a vacancy, HR sees one row per
candidate with candidate name, match status, notes summary, review status, and Send/Delete
actions (Send may be a placeholder until templates exist). Rows navigate into the review
workspace. The header shows the vacancy title with date and a "Send Email to all candidate"
button (disabled until ticket 07).

**Blocked by:** 03-eml-import

**Status:** in-progress

- [x] List candidates of a vacancy: name, match status, notes, review status
      (match status renders "—" until ticket 05 computes skills-vs-requirements)
- [x] Delete a candidate
- [ ] Row opens the review workspace for that candidate — deferred: review route
      lands with ticket 06-review-workspace
- [x] Header shows vacancy title with date and a (disabled) "Send Email to all candidate" button
- [x] Backend and frontend tests pass (written after implementation)

**Amendment (2026-08-31):** the S2 drop zone moved into an "Import .eml" dialog on the
candidate list page (user request) instead of an inline-only empty state; the empty state
now offers a button that opens the same dialog.
