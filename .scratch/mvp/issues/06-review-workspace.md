# 06: Review workspace

**What to build:** The review screen (S4): side-by-side workspace for one candidate at a
time. Left: vacancy skills requirements panel, candidate's extracted data, the original
email subject/body (expandable), and a notes editor with save. Right: multi-page PDF viewer
with pagination. Bottom action bar: Prev / Accept / Flag / Reject / Next, with a position
indicator (e.g. 1/30). Accept/Flag/Reject set the candidate's review status; vacancy
progress numerator advances.

**Blocked by:** 04-candidate-list, 05-pdf-extraction-match-status

**Status:** ready-for-agent

- [ ] PDF viewer renders multi-page CVs with pagination controls
- [ ] Vacancy skills requirements and candidate extracted data shown beside the PDF
- [ ] Original email subject and body viewable
- [ ] Notes editable and saved per candidate
- [ ] Accept / Flag / Reject set review status; Prev / Next navigate candidates; position indicator accurate
- [ ] Vacancy progress reflects reviewed candidates
- [ ] Backend and frontend tests pass (written after implementation)
