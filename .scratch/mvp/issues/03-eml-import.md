# 03: Import .eml files into a vacancy

**What to build:** The import screen (S2): from a vacancy, HR drops one or more exported
`.eml` files onto a drop zone. Each email becomes a candidate belonging to that vacancy,
preserving the email's subject, body, and sender, and storing any PDF attachments as the
candidate's CV document(s). The vacancy list progress denominator reflects imported
candidates.

**Blocked by:** 02-vacancy-crud

**Status:** ready-for-agent

- [ ] Drop zone on a vacancy accepts multiple `.eml` files
- [ ] Each `.eml` creates one candidate with subject, body, and sender preserved
- [ ] PDF attachments are extracted and stored as the candidate's CV document
- [ ] Vacancy progress shows total imported candidates
- [ ] Backend and frontend tests pass (written after implementation)
