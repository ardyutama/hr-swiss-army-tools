# Intake rounds as the candidate-owning unit

Recruitment fills one vacancy in waves: collect a wave of applications, review, hire,
then open another wave if slots remain. A flat per-vacancy candidate list cannot express
which wave produced which hires, and mixes fresh applicants with exhausted leftovers.

We decided candidates are owned by an **Intake Round** (a named wave of application
intake) inside the vacancy, not by the vacancy directly. Every vacancy always has rounds
(a vacancy without waves lives in one default round); exactly one round is active at a
time; rounds are created and closed manually and never reopen. Candidate identity narrows
to person + round + source email. Needed Hires and shortage stay vacancy-level rollups —
rounds carry no quota. This deliberately reverses the V2 spec's original "no named batch
entities" out-of-scope line.

**Considered options:** (a) intake rounds as described; (b) dated interview sessions over
a vacancy-level pool — a different, smaller feature that doesn't restructure candidates;
(c) quota-only groupings — rejected, it reintroduces per-batch quotas and their rollup
ambiguities. (a) is the only option consistent with per-wave candidate lists and review.

**Consequences:** the candidate FK, import, lists, filters, and the review workspace all
re-home from vacancy to round (routes gain `:roundId`); existing vacancies migrate into a
default Round 1; vacancy-detail becomes a rollup (header + round list + active round's
candidates). Closed rounds are read-only; leftovers reach a new wave only via explicit
Promote (a move, not a copy). A runaway's reopened slot belongs to the vacancy shortage —
the active round's implicit goal — not to its origin round.
