# 07: Send via mailto (single and bulk)

**What to build:** Wire up sending without server-side SMTP: the per-candidate Send action
and the "Send Email to all candidate" / "Send To All" buttons render the appropriate
template (Shortlisted or Rejected, matching each candidate's review status) with the
candidate's data, and open a `mailto:` link (or copy to clipboard as fallback) so HR sends
from their own email client.

**Blocked by:** 06-email-templates

**Status:** implemented — architecture refactor applied (grill 2026-09-15, candidates 1–3 of the architecture review)

- [x] Per-candidate Send opens a rendered-template mailto matching the candidate's status
- [x] Send To All covers every shortlisted candidate with the Shortlisted template and every rejected candidate with the Rejected template
- [x] Clipboard fallback when mailto is unavailable
- [x] Candidates missing an email address are flagged, not silently skipped
- [x] Backend and frontend tests pass (written after implementation)

---

## Handoff: Contactability refactor (grill of 2026-09-15, frontier exhausted)

Architecture review candidate **1 — collapse the Contactable Candidate rule into one
classification module** — grilled in two rounds; every decision below is settled. Implement
as written; do not reopen. The rule today is re-expressed seven times across two features
(priority if-chain, three filter helpers, two count helpers, three unchecked
`reviewStatus as EmailTemplateKind` casts) — drift risk is the defect; the casts are the
proof case.

### Settled decisions

- **Union:** `Contactability` — four variants: `undecided` (new/flagged), `outcome-recorded`
  (shortlisted with a hire outcome), `missing-email` (rejected, or shortlisted without
  outcome, and no contact email), `contactable`. Variants are mechanic-named; `CONTEXT.md`
  is **untouched**. The four categories are mutually exclusive and exhaustive — the old
  if-chain's priority order is cosmetic; the union's variant order carries the copy order.
- **Payload:** the `contactable` variant carries `decidedAs: 'shortlisted' | 'rejected'`,
  structurally identical to `EmailTemplateKind` (candidates/format.ts needs **no**
  email-templates import). This deletes all three casts; shortlisted-with-outcome can never
  classify as contactable-shortlisted — a type fact, not a branch coincidence.
- **Home:** both entry points in `src/hr-sat.Client/src/features/candidates/format.ts`.
  The plan is "partition a candidate list by the Contactable Candidate rule," not
  send-specific; any future surface needs it identically.
- **Interface (exactly 4 new exports, 5 old ones delete outright):**
  - `Contactability` (type)
  - `contactability(candidate)` — classifier; input type keeps today's structural shape
    (`{ reviewStatus, hireOutcome, contactEmail }`)
  - `contactabilityPlan(candidates)` — one pass, **full** four buckets as `T[]`
    (generic like today's list helpers): `{ contactable, missingEmail, undecided,
    outcomeRecorded }`; counts are `.length` at the call site
  - `contactBlockReason(classification)` — **total** over the blocked variants, returns
    `string` (no null); exhaustive three-branch switch, no default, so a fifth variant
    fails compilation. Callers narrow first (TS narrowing hands the else-branch the
    blocked type; no `BlockedContactability` alias export needed)
  - **Delete:** `isContactable`, `contactableCandidates`, `missingEmailCandidates`,
    `undecidedCount`, `outcomeRecordedCount`. No aliases.
- **Copy frozen byte-identical:** `Not yet shortlisted or rejected`,
  `Hire outcome already recorded`, `Needs an email address`, and the `Send email — `
  prefix — the spec's aria-label contract depends on it.
- **`usePreparedMessages` public surface frozen** (`contactableCount`, `missingEmail`,
  `excludedUndecided`, `excludedOutcome` stay). Internals: one `plan` computed replaces the
  four scans; `missingTemplateKinds` and `load()` re-derive `decidedAs` via
  `contactability(candidate)` per row (cheap pure call) and narrow — **no payload threading
  through plan buckets, no casts**. `PreparedMessageListDialog` untouched.
- **`CandidateList`:** `sendLabel` classifies once — contactable → `Send email`, otherwise
  `Send email — ${contactBlockReason(c)}`; `:disabled` becomes the component-local
  `contactability(candidate).kind !== 'contactable'`.
- **No ADR** (reversible, unsurprising); this section is the record.

### Implementation order

1. **Characterization first** ([testing.md](../../../docs/agents/testing.md) allows
   pure-function unit tests for traceable business rules; precedent `hiring.spec.ts`):
   create `src/hr-sat.Client/src/features/candidates/contactability.spec.ts` **against the
   current helpers** — it must pass immediately. Cover: full truth table (4 review
   statuses × hire outcomes × email present/absent → which helper reports what) and
   partition invariants (every candidate lands in exactly one bucket; buckets concatenate
   to the input list).
2. Rewrite [format.ts](../../../src/hr-sat.Client/src/features/candidates/format.ts) onto
   the union + plan + total copy mapper; port the spec to the new API in the same commit.
3. Update [usePreparedMessages.ts](../../../src/hr-sat.Client/src/features/prepared-messages/usePreparedMessages.ts)
   internals per above (plan computed, `decidedAs` narrowing, casts die).
4. Update [CandidateList.vue](../../../src/hr-sat.Client/src/features/candidates/components/CandidateList.vue)
   `sendLabel` + `:disabled` binding.
5. Leave the 2 pre-existing round-close spec failures failing — out of scope.

### Acceptance checks

- `npm --prefix src/hr-sat.Client run test -- VacancyDetailView` → same 46 pass / 2
  pre-existing round-close fails as before the refactor.
- New `contactability.spec.ts` green.
- `npm --prefix src/hr-sat.Client run typecheck` (or `vue-tsc`) clean — in particular, zero
  remaining `as EmailTemplateKind` casts in prepared-messages.
- Grep finds no live references to the five deleted helpers.
