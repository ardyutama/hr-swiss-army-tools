# 03: Screening rules (computed Screened Out, never deletion)

**Blocked by:** 02-form-layout-config

**Status:** ready-for-agent

**Terms:** Screening Rule, Screened Out — [CONTEXT.md](../../../CONTEXT.md).
**Decision:** [ADR-0014](../../../docs/adr/0014-screening-rules-as-computed-disposition-never-deletion.md).

## What to build

Vacancy-owned **Screening Rules** over stored raw Form Responses, computing a
**Screened Out** disposition. Rules are edited from the vacancy detail ("Screening"
section) and evaluate **live** (read path), so editing reclassifies without re-import.

- Up to **5** rules per vacancy, **AND**-combined, each over one form column:
  `equals` / `not-equals` / `is-empty` / `not-empty` / `contains` on **raw text** (no type
  parsing — salary free text like "Bacik" / "5,3" / "6000.000-7000.000/month" stays text).
- **Never deletion:** every row is imported and stored. Screened Out is a disposition
  layered on Review Status (which stays `new`).
- **Candidate list:** screened-out candidates are excluded from the default list;
  a toggle with a count badge ("37 screened out") reveals them, each row showing which
  rule(s) fired as chips. No delete affordance anywhere.
- **Live count safety net:** the rules editor shows "would screen out N of M" before save;
  a rule matching nothing or everything gets an amber warning.
- **Email candidates are never screened** (no form columns).
- **Settles at round closure** (ADR-0010): after closure, rule edits affect only the
  active round and future imports.
- The candidate list moves to **server-side pagination (100/page)** because screening
  exclusion can no longer assume "fetch everything".

## Out of scope

Bulk-reject-the-screened-out action (future); type-aware rules (deferred per ADR-0014).

## Seam tests

- Rule `K equals "Tidak"` screens out matching form candidates; they stay stored.
- Empty-column rule (`is-empty`) matches empty CV-link / experience columns.
- Screened-out excluded by default; toggle reveals with count + rule chips.
- Editing rules reclassifies live (a fixed rule re-surfaces its victims).
- Closed round: screening status frozen; rule edits don't reclassify the closed round.
- Email-sourced candidate in a mixed round is never screened.
- Live count is correct before save.

## Acceptance

- [ ] "Screening" editor on vacancy detail, ≤5 AND rules, text operators
- [ ] Computed Screened Out disposition; Review Status untouched
- [ ] Default list excludes screened-out; toggle + count badge + rule chips
- [ ] Live "would screen out N of M" + amber all/nothing warning
- [ ] Server-side 100/page list with screening applied server-side
- [ ] Settles at round closure
- [ ] Backend + frontend seam tests pass (written after implementation)
