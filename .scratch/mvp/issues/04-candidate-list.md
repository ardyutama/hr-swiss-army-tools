# 04: Candidate list per vacancy

**What to build:** The candidate list screen (S3): from a vacancy, HR sees one row per
candidate with candidate name, match status, notes summary, review status, and Send/Delete
actions (Send may be a placeholder until templates exist). Rows navigate into the review
workspace. The header shows the vacancy title with date and a "Send Email to all candidate"
button (disabled until ticket 07).

**Blocked by:** 03-eml-import

**Status:** ready-for-agent

- [ ] List candidates of a vacancy: name, match status, notes, review status
- [ ] Delete a candidate
- [ ] Row opens the review workspace for that candidate
- [ ] Header shows vacancy title with date and a (disabled) "Send Email to all candidate" button
- [ ] Backend and frontend tests pass (written after implementation)
