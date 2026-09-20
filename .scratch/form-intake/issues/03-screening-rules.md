# 03: Screening rules (computed Screened Out, never deletion)

**Blocked by:** 02-form-layout-config

**Status:** ready-for-agent — design settled across two grill sessions on 2026-09-19
(domain/API decisions 1-20; client decisions 21-34 plus assumptions A1-A5; decision 10
amended for the messaging consumer; all recommendations accepted by the product owner).
Backend verified substantially landed the same date: rule-set CRUD, preview, evaluation
+ freeze at closure, paged list with counts and the `screened` filter, review-queue,
promote-summary, and the delete guard are all in. The messaging summary endpoint
(decision 34) landed 2026-09-19 — `GET …/rounds/{id}/messaging-summary` over
`CandidateSummaryMapper`, unpaged, screening fields included, with its HTTP-seam test —
completing the backend. The client rewrite has not started.
UI shape pinned by `03-screening-rules.layout.md` (Nuxt UI v4, ADR-0008 language;
neutral surfaces — amber stays reserved for Flagged / Lifecycle Conflict). CONTEXT.md
(Vacancy Progress), ADR-0014 (stored freeze verdict), and ADR-0008 (design-taste
binding subset) amended during the sessions.

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
  exclusion can no longer assume "fetch everything" — and with it the whole filter/sort/
  count complex (status filter, outcome facet, text query, received sort, toolbar counts)
  moves server-side. The review queue and promote dialog get their own unpaged summary
  endpoints.

## Grill decisions (2026-09-19, confirmed by product owner — all recommended options accepted)

Domain model:
1. Storage: new vacancy-owned `ScreeningRuleSet`, one row per vacancy (`screening_rule_set`
   table, unique `vacancy_id`), jsonb array of `{ordinal, operator, value}` ordered by
   index; GET/PUT under `/api/vacancies/{id}/screening-rules`. Mirrors the `FormLayout`
   precedent (issue-02 grill #1). One `Validate` owns invariants: ≤5 rules, ordinal ≥ 0,
   value required and non-empty iff operator is `equals`/`not-equals`/`contains`, value
   absent for `is-empty`/`not-empty`.
2. Rules target **any ordinal in the header snapshot**, independent of picked columns
   (ADR-0013: screening and layout are separate read-time projections over stored raw
   data). The editor offers snapshot columns (header text, plus Column Label when one
   exists). Rule editing requires a snapshot, so before the first import completes the
   editor shows an empty state ("import form responses first").
3. Matching semantics: trim cell and rule value; `equals`/`not-equals`/`contains` are
   **case-insensitive** (invariant culture); `is-empty`/`not-empty` treat null,
   whitespace-only, and short-row (row shorter than the ordinal) as empty; resubmitted
   candidates evaluate against the **current** Form Response (`IsCurrent`) only — hidden
   prior submissions are never screened.
4. Freeze at closure: `CloseRound` evaluates once and stores per form candidate
   `screened_out` bool + verdict descriptor jsonb (`[{index, display}]` — the same shape
   as the list chips). Closed-round reads return the frozen verdict; active-round reads
   evaluate live; rule edits after closure never reclassify a closed round. ADR-0014
   amended 2026-09-19: "runs in the read path" now includes one write at the lifecycle
   moment, consistent with ADR-0010's settled-data model.
5. Promote sheds the frozen verdict: the candidate re-evaluates live in the active round
   (still-matching rules re-screen; a fixed rule re-surfaces them). Promote preserves only
   review status, notes, and requirement reviews — the screening verdict is not in that
   set.
6. Evaluation lives in the domain: a pure function on the rule set
   (`Evaluate(cells) → fired rule indexes`), unit-tested without EF; `CloseRound` and the
   list query both call it. The jsonb/SQL translation for the paged list lives in
   Infrastructure.
7. Deletion guard: candidate removal refuses a **currently** screened-out candidate (409,
   copy names the fired rule); the UI hides the delete affordance on screened rows.
   Closed-round candidates are already undeletable via `EnsureCanRemoveCandidate` — the
   guard matters only in the active round. A re-surfaced (fixed-rule) candidate is
   removable again: the guard evaluates the current disposition.
8. Vacancy Progress excludes the screened-out (CONTEXT.md amended 2026-09-19): progress
   = shortlisted + rejected over all non-screened-out candidates — the screened-out are
   outside HR's decision funnel.

API seam:
9. Chips payload: list and details responses carry `screenedOut: bool` +
   `firedRules: [{index, display}]`; `display` is rendered **server-side** — Column Label
   if present, else header snapshot (truncated ~24 chars), else `Column N` — joined with
   the operator and value (e.g. `Bersedia di departemen mana saja · equals "Tidak"`).
   Frozen verdicts store the same descriptor, so closed-round chips render verbatim even
   after rules are edited or deleted.
10. List contract split: `GET …/rounds/{id}/candidates` becomes the paged contract
    `{ items, page, pageSize: 100, total, filteredTotal, counts: { status, outcome,
    screenedOut } }` with server-side status/outcome/query/sort/`screened` filters.
    The review queue and promote dialog get their own unpaged summary endpoints (they
    don't page and don't filter). Counts: status/outcome computed **after** the
    screened-out exclusion but independent of the status/outcome/query filters (today's
    toolbar semantics preserved); `counts.screenedOut` is always present and independent
    of every filter — it powers the toggle badge. **Amended 2026-09-19 (client grill):**
    messaging is the third unpaged-summary consumer — the prepared-message send flow and
    template preview read the full list today, and a 100-row page would silently shrink
    both; see decision 22.
11. Default-exclusion scopes: the paged list excludes screened-out unless `screened=all`;
    the review queue excludes screened-out; the promote dialog shows **all** eligible
    candidates with a "screened out" chip — the rescue path for a fixed rule, which must
    not be hidden by default.
12. Preview: `POST /api/vacancies/{id}/screening-rules/preview` carrying the **draft**
    rule set returns `{ total, screenedOut, perRule: [{index, screenedOut}] }`; `total` =
    active-round **form** candidates only (email candidates are never screenable, so
    they're out of M). Per-rule amber when a rule screens 0 or all of `total`; the
    combined N counts a candidate failing two rules once. The editor calls it on open and
    debounced on edit.
13. Review page: `GetCandidateDetails` gains a `screening` field (same shape as the list
    field); the workspace renders a **neutral** banner (not amber) "Screened out by N
    rules: [chips]". Screening informs; it never blocks review, flag, or shortlist.
14. Delete refusal → 409 Conflict naming the fired rule via the existing
    `CustomResults.Problem` path; rule-set validation failures → 400 as usual.

Client:
15. Filter state ownership: URL-owned (`status`, `outcome`, `query`, `sort`, `page`,
    `screened=all`; defaults omitted, `page` omitted when 1), initialized via
    `candidateFilterStateFromQuery`; server-response-owned (`items`, `counts`, `total`)
    replaced wholesale per fetch; the search box is a draft debounced ~300ms into URL
    state. Any filter change resets `page` to 1 and refetches; toggling `screened`
    refetches. `useCandidateFilter.filteredCandidates` dies (the server filters now); the
    composable keeps the list-state union + URL sync + facet state, staying inside the
    candidates feature module.
16. Screening editor: a "Screening" section on the vacancy detail — summary line +
    `UModal`, no new route (mirrors form-layout). Enabled once a header snapshot exists;
    read-only when the vacancy is closed; opens without an active round (rules bind
    future imports) with a "no active round" note where the count would be. Rule rows:
    column select ← header snapshot, operator select, value input (hidden for
    `is-empty`/`not-empty`); add at bottom, per-row remove, **no reordering** (AND is
    order-independent); add disabled at 5.
17. Toolbar toggle: "Show N screened out" with the count badge from `counts.screenedOut`;
    screened rows show the rule chips and no delete affordance.

Tests & execution:
18. Backend (xUnit, TestDbContext/ApiFactory): operator semantics per operator
    (short-row = empty, case-insensitivity, trim), AND-combination, email candidates
    never screened, freeze at `CloseRound` + post-closure rule edits don't reclassify,
    paged list (exclusion default, toggle, counts, 100-page boundaries), delete refusal
    409, rule-set validation (>5, operator/value pairing). Domain unit tests for
    `Evaluate`. Client (Vitest view seams, stubbed fetch): Screening section renders;
    toggle reveals chips + badge; pager; editor live N-of-M + amber; closed vacancy
    read-only. No new E2E.
19. One additive migration: `screening_rule_set` table + `candidate.screened_out bool
    not null default false` + `candidate.screening_verdict jsonb null`. No data backfill
    — nothing is frozen until a round closes; the dev-data-sacrifice precedent from issue
    02 stands.
20. Execution order per `docs/agents/workflow.md`: backend slices (schema → rule-set
    CRUD → evaluation + freeze → paged list → delete guard → preview → messaging
    summary), then client (screening feature module → list rewrite → editor), seam
    tests after implementation per this issue's acceptance line. **Amended 2026-09-19
    (client grill round 2):** verified every backend slice except the messaging
    summary is already landed; the order above stands for what remains.

### Client grill, round 1 (2026-09-19, second session — all recommended options accepted)

Companion layout doc amended the same date; ADR-0008 gained the design-taste binding
subset (decision 29).

21. Module boundaries: new `features/screening/` owns the rules flow only (`api.ts` —
    rule-set GET/PUT + preview POST + DTOs; `validation.ts` Zod; `useScreeningRules`
    editor lifecycle with debounced preview; `ScreeningSection` + `ScreeningRulesDialog`
    components). No shared chip/banner components: the server renders chip text verbatim
    (decision 9), so each surface — list row, review banner, promote row — renders a
    one-line UBadge/UAlert locally; a shared component isn't earned. In candidates,
    `filter.ts` shrinks to the URL serializers (now also emitting `page` and `screened`);
    the client-side filter/sort/count helpers are deleted, not deprecated.
    `useCandidateFilter` collapses into `useCandidates` (paged fetch + list-state union)
    plus URL sync.
22. Messaging is the third unpaged-summary consumer (amends decision 10): the
    prepared-message send flow and template preview read the full candidate list today,
    and a 100-row page would silently shrink both. One unpaged round-candidates summary
    shape serves review queue, promote, and messaging, carrying `screenedOut` +
    `firedRules`; eligibility stays per consumer — queue excludes screened unless
    `screened=all`, promote includes all with chips, messaging ignores screening
    (screened-out stay Review Status `new`, already non-contactable; the template
    preview fallback skips them).
23. Review queue mirrors the list's full URL state, `screened=all` included (a
    deep-linked screened row keeps its neighbors), and round-trips `page` so Back
    restores the list exactly. The screening banner stacks above the Prior Application
    Notice (disposition outranks history), is non-dismissible (a persistent disposition,
    not a notification), and renders as a neutral UAlert per the PriorApplicationNotice
    precedent — icon `i-lucide-filter` or none, never the triangle-alert glyph, which
    carries destructive weight on the action bar.
24. Pagination: UPagination below the table at the trailing end plus a leading
    "1-100 of 298" text counter (plain text; density 5-6, no mono). `page` lives in the
    URL, omitted when 1. A stale page (deep link, or reclassification shrank the set)
    renders the one-sentence-plus-one-action empty state — "This page is empty —
    candidates may have been reclassified." + "Back to page 1" — never an auto-redirect.
    Page transitions keep toolbar/table chrome stable and swap in shape-matched skeleton
    rows (ADR-0008 #14); no full-panel spinner.
25. Toggle anatomy: UCheckbox labeled "Show screened out" (subset-inclusion filter
    semantics, not a settings on/off) plus a separate neutral UBadge carrying
    `counts.screenedOut` — stable while other counts shift. Toolbar wraps to two rows
    below `lg` with the toggle pinned trailing on row one (declared collapse; shared
    office PCs at 1280px hit the wrap). Status/outcome chip counts render the server
    payload; no client recomputation survives.
26. Chips stack inside the Candidate cell (name on line one, chips below): no new
    column, no colgroup reflow on toggle, row height grows only on screened rows, and
    the semantic table stays intact (ADR-0008 #10). Each chip truncates to one line with
    a UTooltip carrying the full server string; at most 2 chips render, the rest
    collapse into a "+N more rules" chip whose tooltip lists all. One `·` per chip line —
    within the middle-dot ration.
27. Editor feedback tiers: Zod validation errors render inline under the rule row on
    submit (UForm precedent); per-rule amber all/nothing warnings move from the footer
    to inline under the rule they name (layout mock amended); the N-of-M count stays in
    the footer. Preview is strictly advisory — latest-wins debounce discards stale
    responses, a failure renders "Live count unavailable" in the count region and never
    blocks Save. Save stays modal-atomic: PUT → close → success toast → one refresh
    announcement reloads candidates and the vacancy progress header in parallel.
28. The summary line counts the viewed round and names it: "2 rules · 37 screened out
    in Round 2." With no round selected the clause drops. The editor's preview keeps its
    own explicit "in the active round" phrasing; the two never share a sentence shape.
29. Design-taste binding subset (ADR-0008 amended): binds — full state cycles
    (loading/empty/error per region), inline errors never toast-only,
    one-sentence-plus-one-action empty states, WCAG AA contrast on every control, no
    dead UI, emoji-free UI (`i-lucide-triangle-alert` on a warning UAlert replaces ⚠),
    shape/color/density locks, middle-dot ration ≤1 per line. Does not bind — the
    em-dash ban (the repo's copy voice predates the skill: `Round 1 — First wave`,
    `Send email — <reason>`), hero/bento/marquee/motion choreography, dark-mode dual
    design.

### Client grill, round 2 (2026-09-19 — all recommended options accepted; assumptions A1-A5 unchallenged)

30. Toggle at zero: the "Show screened out" checkbox renders **disabled** when the
    viewed round's `counts.screenedOut` is 0, with the tooltip "No screened-out
    candidates in this round" — unavailable-but-explained (the Send-button precedent),
    never hidden, so the toolbar stays spatially stable as imports land. The count is
    filter-independent (decision 10), so the control doesn't flicker while HR filters.
31. Round switch drops `page` and only `page`; `status`/`outcome`/`query`/`screened`
    persist — HR compares rounds through the same lens, and today's component-state
    filters already persist across switches. The overflow empty state (decision 24) is
    reserved for genuine races (reclassification, removal), not routine navigation.
32. Editor edge cases. (a) Orphaned ordinals: a drift confirmation can shrink the
    header snapshot, stranding a saved rule's ordinal; the column select keeps the
    orphaned option rendered as "Column N — not in the current header snapshot",
    keepable or removable, never a validation block — the server still evaluates the
    rule (short-row = empty, decision 3) and the amber all/nothing net flags the dead
    rule from the preview. (b) M=0: an active round with zero form candidates replaces
    the count line with "No form candidates in the active round to screen." and
    suppresses per-rule amber — 0 of 0 is not a signal. The editor's third distinct
    count state, beside "no active round" and the pre-snapshot disabled state.
33. Query scope expands to typed contact email and phone: the match runs server-side
    over stored Candidate Details, no response-shape change needed. Decision 10's
    "toolbar semantics preserved" covers filter/count behavior; the query contract is
    written for the first time here, so the known form-round gap (a form candidate's
    identifiers unsearchable today) is fixed rather than baked in. The placeholder
    adapts: "Name, email, or phone" when the vacancy has a form layout, today's
    "Name, sender email, or subject" otherwise.
34. Messaging summary endpoint is built now, not deferred: `GET
    /api/vacancies/{id}/rounds/{id}/messaging-summary`, mirroring the review-queue and
    promote-summary shape (unpaged CandidateSummary list, screening fields included).
    The prepared-message send flow and template preview switch to it, so the 100-row
    page never silently shrinks "Send email to all candidates". Verified 2026-09-19:
    every other backend slice of this issue is already landed — this endpoint is the
    one missing piece.

Assumptions (stated in round 2, unchallenged):

- A1: screened rows stay fully row-clickable into review (decision 13); only the
  delete affordance disappears — Send keeps following the existing contactability rules.
- A2: closed-round lists render the toggle/chips identically, sourced from frozen
  verdicts — no client special-casing beyond rendering the payload.
- A3: the promote dialog shows one neutral "screened out" badge inline after the name,
  not per-rule chips — the dialog is a rescue path, not a diagnostic.
- A4: column-select options render `ordinal · label` when a Column Label exists, else
  `ordinal · header` truncated ~48 chars.
- A5: editor save button reads "Save rules"; cancel/close discards with no
  dirty-confirm — modal-atomic per the form-layout precedent.

## Out of scope

Bulk-reject-the-screened-out action (future); type-aware rules (deferred per ADR-0014);
rule reordering (AND is order-independent — a pure-UI follow-up if ever wanted).

## Seam tests

Backend:
- Operator semantics: `equals`/`not-equals`/`contains` case-insensitive with trim;
  `is-empty` matches null, whitespace, and rows shorter than the ordinal; `not-empty`
  inverse.
- Rule `K equals "Tidak"` screens out matching form candidates; they stay stored and
  Review Status stays `new`.
- AND-combination: a candidate failing two rules is counted once; chips list both rules.
- A resubmitted candidate evaluates against the current response only.
- An email-sourced candidate in a mixed round is never screened.
- Editing rules reclassifies the active round live (a fixed rule re-surfaces its victims).
- `CloseRound` freezes verdicts; later rule edits don't reclassify the closed round, and
  its chips still read the rule text that fired at closure.
- A promoted screened-out candidate re-evaluates live in the active round.
- Delete of a currently-screened-out candidate → 409 naming the rule; after a rule fix
  re-surfaces them, delete succeeds.
- Paged list: 100/page boundaries, exclusion by default, `screened=all` toggle, counts
  independent of active filters, `counts.screenedOut` correct.
- Rule-set validation: >5 rules, missing value for `equals`, value present for
  `is-empty`, negative ordinal → 400.
- Preview: N of M correct (M = active-round form candidates only); per-rule counts
  correct; per-rule 0/all flags correct.
- Messaging summary returns the active round's full unpaged list with screening
  fields — a >100-candidate round comes back whole.
- Vacancy Progress excludes the screened-out.

Client (view seams, stubbed fetch):
- Screening section shows the rule summary; the editor opens, live N-of-M renders,
  per-rule amber all/nothing warnings show, and Add is disabled at 5 rules.
- The toggle reveals screened rows with the count badge and chips; screened rows show no
  delete affordance.
- The pager drives the `page` param; filter changes reset to page 1; a deep link
  restores status/outcome/query/sort/page/screened state.
- Closed vacancy: editor read-only. No active round: "no active round" note in place of
  the count. Before the first import: "import form responses first" empty state.
- The review page renders the neutral screened-out banner with chips.
- Messaging reads the unpaged summary (decision 34's endpoint): a >100-candidate round
  sends to every contactable candidate (not page 1), and the template-preview fallback
  never picks a screened-out candidate.
- The review queue mirrors the list URL state (`screened` included) and Back restores
  `page`; a stale page renders the recoverable empty state, never an auto-redirect.
- The toggle renders disabled with its tooltip when the viewed round has no
  screened-out candidates; a round switch drops `page` and keeps the other filters.
- The editor renders an orphaned ordinal as "Column N — not in the current header
  snapshot" (removable, never a validation error); an active round with no form
  candidates shows "No form candidates in the active round to screen." and no amber.
- The search placeholder reads "Name, email, or phone" when a form layout exists,
  "Name, sender email, or subject" otherwise.

## Acceptance

- [ ] `screening_rule_set` per vacancy (one row, jsonb rules), ≤5 AND rules over header-snapshot ordinals, text operators (case-insensitive, trimmed)
- [ ] Pure domain `Evaluate` + Infrastructure jsonb translation for the paged list
- [ ] Computed Screened Out disposition; Review Status untouched; email candidates never screened
- [ ] Default list excludes screened-out; toggle + count badge + rule chips (label/header + operator + value, server-rendered)
- [ ] Freeze at round closure (stored verdict incl. chip text); rule edits reclassify only the active round
- [ ] Promote sheds the verdict; re-evaluation live in the active round; promote dialog lists screened-out eligible candidates with a chip
- [ ] Delete refused (409 naming the rule) for currently-screened-out candidates; affordance hidden on screened rows
- [ ] Server-side 100/page list with status/outcome/query/sort/screened filters + counts server-side; review queue, promote, and messaging (send flow + template preview) on their own unpaged summary endpoints — messaging summary = `GET …/rounds/{id}/messaging-summary` (decision 34)
- [ ] Query matches name, sender email, subject, and typed contact email/phone; placeholder adapts to form-layout presence
- [ ] UPagination + position counter; a stale page renders a recoverable empty state ("Back to page 1"), never an auto-redirect; skeleton rows between pages
- [ ] Fired-rule chips stack in the Candidate cell (≤2, "+N more rules" tooltip); toggle = UCheckbox + separate neutral count badge, disabled with tooltip at zero; round switch resets `page` only
- [ ] Editor handles orphaned ordinals (keepable, amber-flagged) and the no-form-candidates count state (amber suppressed)
- [ ] Vacancy Progress excludes the screened-out
- [ ] "Screening" editor on vacancy detail (UModal, no route) with live "would screen out N of M" + per-rule amber all/nothing warnings
- [ ] Review-page neutral banner for screened-out candidates (never blocks human action)
- [ ] Backend + frontend seam tests pass (written after implementation)
