# 01: Send to all — server-side dispatch (US-19)

**What to build:** Turn the messaging console's "Send To All" into real sending: the server
dispatches the rendered template email to every contactable candidate of the round over the
installation's own SMTP account, and reports per-candidate outcomes. The existing mailto /
clipboard Prepared Message flow stays as-is.

**Source story:** US-19 ("one email template per vacancy … sent to all in one action").
Issue `mvp/07-send-mailto.md` delivered template render + mailto; this issue is the
server-dispatch slice that story always implied.

**Status:** design settled 2026-09-20 (grill, 3 rounds, decisions 1–15); ADR-0019 written
same day — implementation not started

---

## Grill decisions — 2026-09-20 (round 1: audience, mechanism, scope, template guard)

1. **Audience = Contactable Candidates only.** "All" means every *contactable* candidate of
   the round: Rejected Candidates and Bench members (shortlisted, no hire outcome) with a
   contact email recorded. Undecided (new/flagged), outcome-recorded, and missing-email
   candidates are excluded and **named with their reason** in the send report — the
   glossary's Contactable Candidate rule stands unchanged.
2. **Mechanism = true server-side SMTP dispatch.** Brand-new capability — no send command
   exists in the backend today. A per-installation SMTP account is a hard prerequisite
   (the product ships self-hosted on-prem; each customer configures their own). Mail
   reputation and deliverability belong to the customer's SMTP account, by design.
3. **Scope = one round, never cross-round.** Candidates are owned by Intake Rounds
   (ADR-0010) and the same person is a *different candidate* in another round, so
   whole-vacancy sending would double-mail people. Which round (active-only vs. the round
   being viewed) is pinned in round 2.
4. **A missing template kind blocks the whole send.** If any contactable bucket
   (shortlisted/rejected) lacks its template, the send refuses with an amber warning
   naming the missing kind — no partial silent sends. Matches the existing console guard.

## Grill decisions — 2026-09-20 (round 2: mechanism, persistence, execution)

5. **Canonical term: Dispatch / Dispatch Run** (CONTEXT.md amended inline this session).
   A Dispatch is one server-sent email to one contactable candidate; a Dispatch Run is one
   Send To All action's batch. Prepared Message keeps its mailto meaning untouched.
6. **SMTP settings live in appsettings.json / environment variables**, read at startup —
   per-installation infrastructure config, beside the connection string. No settings UI,
   no credentials in the domain DB. In-app settings can come later without rework.
7. **Unconfigured SMTP = amber refusal, not hidden, not silent fallback.** The Send To All
   action stays visible and refuses with actionable copy ("email sending is not
   configured…"); the mailto flow keeps working untouched.
8. **Execution: synchronous, in-request, one candidate at a time.** Per-candidate
   try/catch; one failure never aborts the run. Idempotency unit = the per-candidate
   Dispatch row: re-running sends only to candidates with no successful Dispatch — a
   double-send is structurally impossible.
9. **Persistence: two tables.** `dispatch_runs` (run: vacancy, round, timestamps, counts)
   + `dispatches` (candidate, template kind, **rendered subject/body snapshot**, status,
   error text, timestamps). The snapshot survives later template edits — "what exactly did
   we send this person" is answerable months later.
10. **Round rule amended from decision 3: the viewed round, open or closed** — matching the
    existing mailto behavior. Round Closure settles review data; dispatch writes its own
    records. A closed *vacancy* refuses with an amber Lifecycle Conflict (the hiring
    effort has ended).

## Grill decisions — 2026-09-20 (round 3: re-contact, report, API, visibility)

11. **Re-contact policy: per-Candidate-row idempotency + re-notify warning.** A candidate
    row with a successful Dispatch is never re-sent (decision 8); a *new* row (promoted,
    re-imported) is a fresh start per the Candidate definition. Clicking Send To All on a
    round with any successful Dispatch raises a confirmation ("this round was already
    notified on <date> — continue?") reusing the outcome-confirm-dialog pattern.
12. **Send report = results dialog after the spinner, no new page.** Counts
    ("N sent, M failed, K excluded"), failed rows with error text and a retry action
    (the resume-failed endpoint), excluded candidates grouped by their contactability
    reason. A persistent dispatch-history surface is deferred — the decision 9 schema
    already supports it without rework.
13. **API shape: `POST /api/vacancies/{vacancyId}/rounds/{roundId}/dispatches`** (empty
    body — the audience is derived per decision 1) → `201` with run summary +
    per-candidate outcomes; retry = `POST …/dispatches/{runId}/retry`. Feature folders:
    `Features/Dispatches/SendToAll/` + `Features/Dispatches/RetryFailed/`.
14. **Screened Out candidates never appear in the report.** They are filtered from the
    audience before contactability classification — outside HR's decision funnel, same
    posture as Vacancy Progress. The report's exclusion buckets are exactly the
    contactability union: undecided, outcome-recorded, missing-email.
15. **Per-candidate "Dispatched" indicator in the messaging console.** The
    messaging-summary response gains `lastDispatch: { status, at } | null`; the recipient
    list shows a display-only indicator (never blocks, same posture as Prior Application
    Notice / Resubmitted). Scoped to the console only — the main candidate list is a
    different flow.

### Facts established during exploration (2026-09-20)

- No `Send*` command/handler exists anywhere in the backend; email-template endpoints are
  Get/Upsert/Delete/Render/Sources only.
- Recipient data source: `GET /api/vacancies/{vacancyId}/rounds/{roundId}/messaging-summary`
  (unpaged, screening-aware via the ADR-0017 seam).
- Client classification: `contactabilityPlan()` in
  `src/hr-sat.Client/src/features/candidates/format.ts` — four exhaustive buckets.
- Rendering is server-side only: `POST …/email-templates/render`, placeholders
  `{{ candidate_name }}` / `{{ vacancy_title }}`.
- Bulk-operation precedent: `PromoteCandidatesCommand` (batch IDs, one transaction).
- No background-job infrastructure exists in the codebase.
