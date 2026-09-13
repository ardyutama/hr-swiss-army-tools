# 05: Outcome HUD — menu-in-bar consolidation, Shift-chords, closed-round status pills

**What to build:** A redesign of the review workspace's sticky action bar so the hire
outcomes (Hired / Runaway / Declined) collapse from a peer button cluster into a single
**menu-in-bar** button, the outcome shortcuts move to Shift-chords, and a closed round
renders review status (and a settled hire outcome) as a read-only status pill in the
page header instead of as clickable actions.

> **Supersession note (grill-with-docs, 2026-09-11):** This ticket was originally
> settled on 2026-09-10 with the outcome cluster *promoted to peer styling of S/F/R*
> ("same weight, same size, same solid-when-current"). Before any implementation
> started, the 2026-09-11 grill session re-examined the ticket-02 bar and found it
> overwhelming: up to 10 controls (`Prev | S F R | Mark Hired | Mark Runaway |
> Mark Declined | Clear | Next | Shortcuts`) and six semantically colored buttons on
> one row. The user settled the **menu-in-bar** direction below. The old
> "promote to peer cluster" plan is retired; everything else (Shift-chords, primary
> Next, header pills, visibility rule) carries over.

## Settled decisions (2026-09-11 grill, user confirmed all recommendations)

**(a) Bar story — one unified sticky HUD bar, menu-in-bar.** Keep ONE sticky bottom
bar (`ReviewActionBar`); do not split into a second floating chip and do not relocate
outcomes to the header (that option was considered and rejected: it would re-split the
action surface deliberately unified on 2026-09-10). The bar's visual story is
`back → decide → outcome → forward`:

- Left: **Prev** (neutral outline).
- Middle: **Shortlist / Flag / Reject** (S/F/R, semantic success/amber-700/error per
  ADR-0008 #2 amendment), then the **hire-outcome menu button** whenever the candidate
  is shortlisted.
- Right: **Next** becomes the ONLY `primary`-blue solid button on the bar; forward is
  the dominant move in triage, so blue = advance everywhere and nothing else on the
  bar may use primary. Shortcuts help button stays at far right, neutral.
- Result: max 7 controls (`Prev | S F R | Outcome▾ | Next | Shortcuts`), 4 colors
  (success/amber/error + one blue).

**(b) Menu button as status display.** The collapsed button both invites the action
and shows the current outcome (the old "solid when current" glanceability must not be
lost):

- `hireOutcome === 'none'`: neutral outline reading **"Set outcome ▾"** (verb, per
  ADR-0008 #11).
- Set: adjective label with a **subtle soft semantic tint** (not solid) + small icon —
  soft-green **"Hired"**, soft-red **"Runaway"**, gray **"Declined"**. Subtle/soft is
  status language under ADR-0008 (status colors reserved for semantic state); solid
  would rebuild the color wall this redesign removes. Primary blue stays exclusive to
  `Next`.

**(c) Menu item anatomy.** Items keep the ticket-02 verbs: `Mark Hired`,
`Mark Runaway`, `Mark Declined` — each with its `⇧H` / `⇧U` / `⇧D` hint right-aligned
(the menu is where mouse users *discover* the chords). Then a separator, then
`Clear outcome` (mouse-only, no confirm — fully reversible per ticket 02).

- Transition gating (`Runaway` only from `hired`; `Declined` only from `none`;
  `Hired` not when already `hired`) renders as **disabled menu items, not hidden** —
  a grayed `Mark Runaway` silently teaches the lifecycle; hidden teaches nothing.
- Consequence copy ("Reopens 1 needed-hire slot." / "Does not reopen a slot.") stays
  **only in the confirm dialog**, not duplicated as menu subtext — one source of truth,
  and the dialog fires before commit on every path (mouse or keyboard).

**(d) Outcome shortcuts move to Shift-chords.** Plain `H`/`U`/`D` go fully inert (no
nag toast). `Shift+H` = Mark Hired (direct, no dialog), `Shift+U` = Mark Runaway (arms
dialog), `Shift+D` = Mark Declined (arms dialog). The chords stay **global** — power
users never touch the menu. Same gating as the menu items. The modifier chord makes
the consequential bookkeeping tactile and cannot fat-finger against S/F/R. The kbd
hints in the menu read `⇧H` / `⇧U` / `⇧D`; the Shortcuts modal gains a "Hire outcomes"
group with the three Shift-chord rows (currently undocumented). `Shift+←`/`Shift+→`
(CV paging) already exist and do not collide.

**(e) Closed-round status pills in the header (carried over unchanged).** When the
round is closed, review status stops being buttons and renders as a full-pill
adjective badge — New / Flagged / Shortlisted / Rejected — in the `ReviewHeader` next
to the position/progress counters. The bar then keeps only what's still actionable:
`Prev | (outcome menu, if vacancy open) | Next`. When the vacancy is also closed, the
hire outcome settles too and renders as a second pill (Hired / Runaway / Declined) in
the same header position, **replacing the menu button**; the bar shrinks to
`Prev | Next` (+ Shortcuts). The existing "This round is closed" banner stays. Pills
are read-only: no click, no shortcut, no hover action. Same `canSetOutcome` gate
(vacancy open AND shortlisted) drives the menu as drove the cluster.

**(f) Outcome visibility rule (unchanged, recorded to stop re-litigating).** The
outcome menu button stays hidden until the candidate is shortlisted — ADR-0008 #15
(no dead UI) overrules always-render-disabled. The menu button appearing on the
S-press is intentional: it announces "this candidate changed what it is," reinforced
by the shortlist toast.

**Already shipped (ticket 02) — do not rebuild, only restyle/re-key:**

- Outcome confirm dialog (`OutcomeConfirmDialog`) for Runaway / Declined: consequence
  text, optional note, autofocus textarea, Enter confirms (empty note confirms),
  Esc cancels. Outcome PUT carries `{ outcome, note? }`; the handler appends the note
  to candidate notes atomically with the outcome write.
- Confirming the dialog advances to the next candidate, matching the S/F/R rhythm.
- Outcome facet (`any / undecided / hired / runaway / declined`) on the candidate
  list, scoped to shortlisted rows, composing with status filter and search.
- `canSetHireOutcome` = vacancy open AND candidate shortlisted; review data freezes on
  round close (`isRoundClosed`) while hire-outcome bookkeeping stays editable until
  vacancy close (CONTEXT.md: Round Closure, Settled, Hire Outcome).

**Design read:** internal keyboard-driven review cockpit, density 8 — the landing-page
rules (bento/hero/motion) do not fire. The single source of visual grammar is
ADR-0008: warm-utilitarian, light-only, Geist, radius 12px / full-pill badges, one
accent (primary blue) per screen. Action bar per ADR-0008 #8 (mouse-first with visible
kbd hints, hit targets ≥ 40px). Verbs on actions, adjectives on status per ADR-0008
#11. The menu (`UDropdownMenu`) is a new UI pattern in this codebase — no existing
dropdown usage; introduce it through Nuxt UI v4 per ADR-0007, no `App*` wrapper.

**Domain rationale (why the menu is domain-honest):** CONTEXT.md separates **Review
Status** ("HR's current decision") from **Hire Outcome** ("the bookkeeping record…
never altered by review-status decisions"). The old bar violated that separation
visually by sitting bookkeeping buttons as peers of decision buttons. Collapsing
outcomes into a menu makes the separation physical: decisions are buttons, bookkeeping
is a menu. No glossary change — the menu is implementation, not domain language.

**Glossary:** Hire Outcome, Hired Candidate, Runaway, Declined, Bench, Round Closure,
Settled, Triage Mode (CONTEXT.md).

**Deferred (recorded so they stop being re-litigated):** kanban pipeline view (build
only when managing 3+ vacancies concurrently with active shortlists, read-only, no
drag-drop); structured decline-reason enum (build only when a real "why do candidates
keep declining" trend emerges across vacancies); bulk-decline of leftover shortlist
(the bench stays deliberate); nag toast on inert plain H/U/D (silence is the design).

**No ADR, no CONTEXT.md change** (settled 2026-09-11): reversible component-level
choice; ADR-0008 #8 already owns bar principles; no new domain terms.

**Blocked by:** none (ticket 02 shipped)

**Status:** ready-for-agent

## Implementation checklist

- [ ] Replace the outcome cluster in `ReviewActionBar.vue` with a `UDropdownMenu`:
      menu button per (b) — "Set outcome ▾" neutral outline when none, adjective +
      subtle soft tint + icon when set; items per (c) — `Mark Hired`/`Mark Runaway`/
      `Mark Declined` with `⇧H`/`⇧U`/`⇧D` right-aligned, transition-gated items
      disabled (not hidden), separator, `Clear outcome`; hit targets ≥ 40px
- [ ] Next re-styled as the only primary-blue solid button on the bar; no other
      element uses primary
- [ ] Shortcuts remapped in `useReviewShortcuts.ts`: `Shift+H` direct,
      `Shift+U`/`Shift+D` arm the dialog; plain `H`/`U`/`D` inert with no toast;
      menu item hints show `⇧H`/`⇧U`/`⇧D`; `ShortcutsHelpModal` gains a documented
      "Hire outcomes" group
- [ ] Closed round: review status renders as a full-pill adjective badge in
      `ReviewHeader` (no decision buttons); vacancy-closed also renders the settled
      hire outcome as a pill replacing the menu button; bar keeps only live actions
      (`Prev | outcome menu? | Next | Shortcuts`)
- [ ] Outcome menu button still hidden on non-shortlisted candidates (no disabled
      state for the button itself)
- [ ] Client seam tests (written after implementation): Shift-chord fires / plain key
      inert; U/D arm dialog, H direct; menu items open dialog / direct per outcome;
      gated items disabled; Clear fires without dialog; confirm advances to next;
      closed round renders status pill and hides decision buttons while outcome menu
      stays live (vacancy open) or pill (vacancy closed); Next is primary; outcome
      menu hidden until shortlisted; menu button shows current outcome adjective
- [ ] Existing backend facts (note append atomicity, note max-length, facet scoping)
      keep passing unchanged
