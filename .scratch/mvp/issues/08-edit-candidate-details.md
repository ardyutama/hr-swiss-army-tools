# 08: Enter and edit candidate details

**What to build:** In-place editing of candidate details in the review workspace (S4).
V1 has no extraction, so this ticket is the *primary* way candidate data gets in — manual
entry, not correction. In the review screen's Candidate Details panel:

- Add / edit name, contact email, contact phone (inline or via small dialog)
- Changes persist via new backend endpoints; the candidate list display name stops
  falling back to the source sender once a name is typed

**Blocked by:** 05-review-workspace (the panel exists there, read-only)

**Status:** ready-for-agent

- [ ] Name, email, phone editable and saved per candidate
- [ ] Typed name wins over the sender-fallback display name everywhere
- [ ] Validation: email format, non-empty name
- [ ] Backend and frontend tests pass (written after implementation)

**Origin:** deferred from the 05-review-workspace design session (2026-09-02); reframed
2026-09-03 (V1 re-slice): with extraction moved to V3, this is manual entry. V3 will
auto-fill these same fields from the primary CV (ADR-0009). The skill add/edit/reorder
scope from the old ticket is gone — Candidate Skill is deleted from the glossary.
