# 05: Outcome HUD — promote outcomes, Shift-chords, closed-round status pills

**What to build:** A redesign of the review workspace's sticky action bar so the hire
outcomes (Hired / Runaway / Declined) become first-class HUD buttons alongside the
S/F/R decision row, the outcome shortcuts move to Shift-chords, and a closed round
renders review status (and a settled hire outcome) as a read-only status pill in the
page header instead of as clickable actions.

**Already shipped (ticket 02) — do not rebuild, only restyle/re-key:**

- Outcome confirm dialog (`OutcomeConfirmDialog`) for Runaway / Declined: consequence
  text ("Reopens 1 needed-hire slot." / "Does not reopen a slot."), optional note,
  autofocus textarea, Enter confirms (empty note confirms), Esc cancels. Outcome PUT
  carries `{ outcome, note? }`; the handler appends the note to candidate notes
  atomically with the outcome write (validator max-length mirrors review notes).
- Confirming the dialog advances to the next candidate, matching the S/F/R rhythm.
- Outcome facet (`any / undecided / hired / runaway / declined`) on the candidate
  list, scoped to shortlisted rows, composing with status filter and search.
- `canSetHireOutcome` = vacancy open AND candidate shortlisted; review data freezes on
  round close (`isRoundClosed`) while hire-outcome bookkeeping stays editable until
  vacancy close (CONTEXT.md: Round Closure, Settled, Hire Outcome).

**Design read:** internal keyboard-driven review cockpit, density 8 — the landing-page
rules (bento/hero/motion) do not fire. The single source of visual grammar is
ADR-0008: warm-utilitarian, light-only, Geist, radius 12px / full-pill badges, one
accent (primary blue) per screen reserved for semantic state. Action bar per ADR-0008
#8 (mouse-first buttons with visible kbd chips, hit targets ≥ 40px). Verbs on buttons,
adjectives on status pills per ADR-0008 #11.

**(a) Promote the outcome cluster in one unified HUD bar.** Keep ONE sticky bottom bar
(`ReviewActionBar`); do not split into a second floating chip. The bar's visual story
is `back → decide → forward`:

- Left: **Prev** (neutral outline).
- Middle: **Shortlist / Flag / Reject** (S/F/R, semantic success/amber-700/error per
  ADR-0008 #2 amendment). The **outcome cluster** (Hired success / Runaway error /
  Declined neutral) renders immediately beside the decision group whenever the
  candidate is shortlisted — same weight, same size, same solid-when-current
  treatment as S/F/R, so outcomes read as peers of decisions, not a muted afterthought.
- Right: **Next** becomes the ONLY `primary`-blue solid button on the bar; forward is
  the dominant move in triage, so blue = advance everywhere and nothing else on the
  bar may use primary. Shortcuts help button stays at far right, neutral.

**(b) Outcome shortcuts move to Shift-chords.** Plain `H`/`U`/`D` go fully inert (no
nag toast). `Shift+H` = Mark Hired (direct, no dialog), `Shift+U` = Mark Runaway (arms
dialog), `Shift+D` = Mark Declined (arms dialog). Same disabled gating as the buttons
(Runaway only from hired; Declined only from none). The modifier chord makes the
consequential bookkeeping tactile and cannot fat-finger against S/F/R. The kbd chips
on the outcome buttons read `⇧H` / `⇧U` / `⇧D`; the Shortcuts modal gains a "Hire
outcomes" group with the three Shift-chord rows (they are currently undocumented).
`Shift+←`/`Shift+→` (CV paging) already exist and do not collide.

**(c) Closed-round status pills in the header.** When the round is closed, review
status stops being buttons and renders as a full-pill adjective badge —
New / Flagged / Shortlisted / Rejected — in the `ReviewHeader` next to the
position/progress counters. The bar then keeps only what's still actionable:
`Prev | (outcomes, if vacancy open) | Next`. When the vacancy is also closed, the
hire outcome settles too and renders as a second pill (Hired / Runaway / Declined) in
the same header position; the bar shrinks to `Prev | Next` (+ Shortcuts). The existing
"This round is closed" banner stays. Pills are read-only: no click, no shortcut, no
hover action.

**(d) Outcome cluster visibility rule (unchanged, recorded to stop re-litigating).**
The outcome cluster stays hidden until the candidate is shortlisted — ADR-0008 #15
(no dead UI) overrules always-render-disabled. The bar reshaping on the S-press is
intentional: it announces "this candidate changed what it is," reinforced by the
shortlist toast.

**Glossary:** Hire Outcome, Hired Candidate, Runaway, Declined, Bench, Round Closure,
Settled, Triage Mode (CONTEXT.md).

**Deferred (recorded so they stop being re-litigated):** kanban pipeline view (build
only when managing 3+ vacancies concurrently with active shortlists, read-only, no
drag-drop); structured decline-reason enum (build only when a real "why do candidates
keep declining" trend emerges across vacancies); bulk-decline of leftover shortlist
(the bench stays deliberate); nag toast on inert plain H/U/D (silence is the design).

**Blocked by:** none (ticket 02 shipped)

**Status:** ready-for-agent

- [ ] Outcome cluster promoted to peer styling of S/F/R in the single sticky HUD bar;
  solid-when-current, semantic colors (Hired success / Runaway error / Declined
  neutral), ≥40px hit targets
- [ ] Next re-styled as the only primary-blue solid button on the bar; no other
  element uses primary
- [ ] Shortcuts remapped: `Shift+H` direct, `Shift+U` / `Shift+D` arm the dialog;
  plain `H`/`U`/`D` inert with no toast; button kbd chips show `⇧H`/`⇧U`/`⇧D`;
  Shortcuts modal gains a documented "Hire outcomes" group
- [ ] Closed round: review status renders as a full-pill adjective badge in
  `ReviewHeader` (no buttons); vacancy-closed also renders the settled hire outcome
  as a pill; bar keeps only live actions (`Prev | outcomes? | Next`)
- [ ] Outcome cluster still hidden on non-shortlisted candidates (no disabled state)
- [ ] Client seam tests (written after implementation): Shift-chord fires / plain key
  inert; U/D arm dialog, H direct; confirm advances to next; closed round renders
  status pill and hides decision buttons while outcomes stay live (vacancy open) or
  pill (vacancy closed); Next is primary; outcome cluster hidden until shortlisted
- [ ] Existing backend facts (note append atomicity, note max-length, facet scoping)
  keep passing unchanged
