# 05: Outcome UX — decline/runaway dialog and outcome facet

**What to build:** Two upgrades to the post-shortlist outcome flow settled in ticket 02.

**(a) Confirm dialog for consequential outcomes.** In the review workspace,
Mark Runaway and Mark Declined open a small confirm dialog: it states the
consequence (runaway: "Reopens 1 needed-hire slot"; declined: "Does not reopen a
slot") and offers an optional free-text note. Hired and Clear Outcome stay
one-click — no dialog. On confirm, the outcome PUT carries `{ outcome, note? }`;
the handler appends the note to the candidate's notes in the same write (the
`ApplyReview(status, notes)` precedent). Notes stay purely HR-authored: no system
trail lines for transitions. Confirming the dialog advances to the next candidate,
matching the S/F/R rhythm; the dialog autofocuses the textarea, Enter confirms
(empty note still confirms), Esc cancels. Shortcuts U/D arm the dialog instead of
firing the mutation; H stays direct.

**(b) Outcome facet on the candidate list.** `useCandidateFilter` gains an outcome
facet with values `any / undecided / hired / runaway / declined`. Picking anything
except `any` implicitly scopes the list to shortlisted candidates (hire outcome
exists only on shortlisted rows). `undecided` is the **Bench** view (CONTEXT.md,
Bench) — the backfill pool for runaways and promote pickers. The facet composes
with the existing review-status filter and search.

Glossary: Hire Outcome, Declined, Bench (CONTEXT.md).

**Deferred (recorded here so they stop being re-litigated):** kanban pipeline view
— build only when managing 3+ vacancies concurrently with active shortlists, and
then as a read-only board (no drag-drop); structured decline-reason enum — build
only when a real "why do candidates keep declining" trend question emerges across
several vacancies (Greenhouse-style rejection-reasons reporting); bulk-decline of
leftover shortlist — the bench stays deliberate.

**Blocked by:** none (ticket 02 shipped)

**Status:** ready-for-agent

- [ ] Outcome endpoint accepts optional note; appended to candidate notes atomically
  with the outcome write; validator extends with a note max-length rule mirroring
  review notes
- [ ] Confirm dialog on Mark Runaway / Mark Declined with static consequence text
  and optional note; Hired and Clear remain one-click
- [ ] Confirming the dialog advances to the next candidate like S/F/R; U/D arm the
  dialog, H stays direct; Esc cancels; empty note confirms
- [ ] Outcome facet (`any / undecided / hired / runaway / declined`) in the candidate
  filter; non-`any` scopes to shortlisted; composes with existing filters
- [ ] Backend and frontend tests pass (written after implementation): HTTP-seam
  facts for note append (atomicity, validation) and facet scoping; client seam
  tests for dialog gating (U/D open, H doesn't), advance-on-confirm, facet values
