# 09: Edit candidate details

**What to build:** In-place editing of the extracted candidate data in the review
workspace (S4), per CONTEXT.md which defines Candidate Details and Candidate Skill as
editable. In the review screen's Candidate Details panel:

- Edit name, contact email, contact phone (inline or via small dialog)
- Add / edit / remove / reorder candidate skills
- Changes persist via new backend endpoints and feed the Match count immediately

**Blocked by:** 06-review-workspace (the panel exists there, read-only)

**Status:** needs-triage

- [ ] Name, email, phone editable and saved per candidate
- [ ] Skills add/edit/remove/reorder; Match recalculates after save
- [ ] Validation: email format, non-empty name, skill phrase non-empty
- [ ] Backend and frontend tests pass (written after implementation)

**Origin:** deferred from the 06-review-workspace design session (2026-09-02): CONTEXT.md
calls Candidate Details and Candidate Skill editable, but no endpoints exist yet and 06
only requires notes editing. Triage speed first, correction workflow second.
