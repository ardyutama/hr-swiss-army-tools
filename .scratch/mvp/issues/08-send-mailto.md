# 08: Send via mailto (single and bulk)

**What to build:** Wire up sending without server-side SMTP: the per-candidate Send action
and the "Send Email to all candidate" / "Send To All" buttons render the appropriate
template (Shortlisted or Rejected, matching each candidate's review status) with the
candidate's data, and open a `mailto:` link (or copy to clipboard as fallback) so HR sends
from their own email client.

**Blocked by:** 07-email-templates

**Status:** ready-for-agent

- [ ] Per-candidate Send opens a rendered-template mailto matching the candidate's status
- [ ] Send To All covers every shortlisted candidate with the Shortlisted template and every rejected candidate with the Rejected template
- [ ] Clipboard fallback when mailto is unavailable
- [ ] Candidates missing an email address are flagged, not silently skipped
- [ ] Backend and frontend tests pass (written after implementation)
