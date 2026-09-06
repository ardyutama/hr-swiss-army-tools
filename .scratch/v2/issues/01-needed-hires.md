# 01: Needed hires on the vacancy

**What to build:** A vacancy gains an optional **Needed Hires** count (`int?`, 1–9999):
settable in the create form, editable while the vacancy is open, stored on the vacancy.
Needed Hires lives on the **vacancy** — intake rounds carry no quota (ADR-0010). When
set, the vacancy list row shows the hiring state derived from it: `active hires /
needed hires` and a shortage note (e.g. "8/10 hired · 2 to go"); when all slots are
filled a "Filled" badge shows. When unset (null), the row renders exactly as V1 —
progress counts only. The vacancy never auto-closes when filled. Closing a round while
the vacancy still has a shortage shows the "slots still open" confirmation.

**Blocked by:** V2 ticket 00-intake-rounds

**Status:** ready-for-agent

- [ ] Create/edit vacancy accepts optional Needed Hires (validated 1–9999, editable while open)
- [ ] Vacancy list shows "n/m hired · k to go" when Needed Hires is set; unchanged when null
- [ ] "Filled" badge when active hires reach Needed Hires; vacancy stays open
- [ ] Round close confirms when shortage remains ("k slots still open — close this round?")
- [ ] Backend and frontend tests pass (written after implementation)

**Note:** until ticket 02-hire-outcomes lands, active hires = 0 and shortage = needed.
