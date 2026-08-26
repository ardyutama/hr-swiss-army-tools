# 07: Email templates per vacancy

**What to build:** The bulk-email dialog (S5) minus sending: for a vacancy, HR manages a
Shortlisted template and a Rejected template (file-based, with View/Delete), and can copy a
template from a previous vacancy via the "Previous Template" pickers. Templates render with
the candidate's data (name, vacancy title) as placeholders.

**Blocked by:** 06-review-workspace

**Status:** ready-for-agent

- [ ] Create/replace a Shortlisted template file for a vacancy; View and Delete work
- [ ] Create/replace a Rejected template file for a vacancy; View and Delete work
- [ ] Copy a template from a previous vacancy
- [ ] Template rendering substitutes candidate name and vacancy title
- [ ] Backend and frontend tests pass (written after implementation)
