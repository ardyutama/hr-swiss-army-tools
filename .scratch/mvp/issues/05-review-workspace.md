# 05: Review workspace

**What to build:** The review screen (S4): side-by-side workspace for one candidate at a
time. Left: vacancy skills requirements panel (manual checklist), the candidate's details
and source sender, the original email subject/body (expandable), and a notes editor with
save. Right: multi-page PDF viewer with pagination. Bottom action bar: Prev / Shortlist /
Flag / Reject / Next, with a position indicator (e.g. 1/30). Shortlist/Flag/Reject set the
candidate's review status; vacancy progress numerator advances.

**Blocked by:** 04-candidate-list

**Status:** ready-for-agent

- [ ] PDF viewer renders multi-page CVs with pagination controls
- [ ] Vacancy skills requirements and candidate details shown beside the PDF
- [ ] Original email subject and body viewable
- [ ] Notes editable and saved per candidate
- [ ] Shortlist / Flag / Reject set review status; Prev / Next navigate candidates; position indicator accurate
- [ ] Vacancy progress reflects reviewed candidates
- [ ] Backend and frontend tests pass (written after implementation)

**Amendment (2026-09-03, V1 re-slice):** V1 has no PDF extraction — "candidate extracted
data" becomes manually entered Candidate Details (ticket 08), and requirements stay a
manual checklist. Computed match arrives in V3 (ADR-0009).
