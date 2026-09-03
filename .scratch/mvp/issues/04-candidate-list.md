# 04: Candidate list per vacancy

**What to build:** The candidate list screen (S3): from a vacancy, HR sees one row per
candidate with display name, notes summary, review status, and Send/Delete actions (Send
may be a placeholder until templates exist). Display name falls back to sender name, then
sender email, then email subject until HR types a real name (ticket 08). Rows navigate
into the review workspace. The header shows the vacancy title with date and a "Send Email
to all candidate" button (disabled until ticket 06).

**Blocked by:** 03-eml-import

**Status:** ready-for-agent

- [ ] List candidates of a vacancy: display name (sender fallback), notes, review status
- [x] Delete a candidate
- [ ] Row opens the review workspace for that candidate — deferred: review route
      lands with ticket 05-review-workspace
- [x] Header shows vacancy title with date and a (disabled) "Send Email to all candidate" button
- [x] Backend and frontend tests pass (written after implementation)

**Amendment (2026-08-31):** the S2 drop zone moved into an "Import .eml" dialog on the
candidate list page (user request) instead of an inline-only empty state; the empty state
now offers a button that opens the same dialog.

**Amendment (2026-09-03, V1 re-slice):** the match column is removed — V1 has no
extraction (match returns in V3, ADR-0009). Display name is a sender-fallback display rule
only; the stored name stays empty until typed.
