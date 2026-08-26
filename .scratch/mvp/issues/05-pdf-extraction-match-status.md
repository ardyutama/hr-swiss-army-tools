# 05: PDF extraction and match status

**What to build:** When a candidate's CV document exists, extract text from the PDF and
derive the candidate's data: name, skills mentioned, email, and phone number. Compute the
match status as keyword overlap between extracted skills and the vacancy's skills
requirement lines. Extraction runs at import time (or on demand for candidates imported
before this lands) and its results surface in the candidate list and review workspace.

**Blocked by:** 03-eml-import

**Status:** ready-for-agent

- [ ] PDF text extraction produces candidate name, email, phone, and mentioned skills
- [ ] Match status is computed from skills vs vacancy requirements
- [ ] Extraction failures degrade gracefully (candidate still listed, fields empty)
- [ ] Match status visible in the candidate list
- [ ] Backend and frontend tests pass (written after implementation)
