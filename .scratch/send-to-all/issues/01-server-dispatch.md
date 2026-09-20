# 01: Send to all — server-side dispatch (US-19)

**What to build:** Turn the messaging console's "Send To All" into real sending: the server
dispatches the rendered template email to every contactable candidate of the round over the
installation's own SMTP account, and reports per-candidate outcomes. The existing mailto /
clipboard Prepared Message flow stays as-is.

**Source story:** US-19 ("one email template per vacancy … sent to all in one action").
Issue `mvp/07-send-mailto.md` delivered template render + mailto; this issue is the
server-dispatch slice that story always implied.

**Status:** design settled 2026-09-20 (grill, 3 rounds, decisions 1–15); ADR-0019 written
same day (amended twice: decisions 20, 31); implementation grill settled 2026-09-20
(round 1 backend decisions 16–21, round 2 client decisions 22–26, round 3 SMTP-settings
decisions 27–31); **user-confirmed 2026-09-20** — layout doc:
`01-server-dispatch.layout.md`; follow-up issue: `02-smtp-settings.md` — ready for
implementation (decision 17 order: migration → SendToAll → RetryFailed → client)

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

---

## Grill decisions — 2026-09-20 (implementation grill, round 1: backend foundations)

16. **Contactability moves to the domain.** A pure domain function
    (`Contactability.Evaluate(reviewStatus, hireOutcome, contactEmail)` →
    `Undecided | OutcomeRecorded | MissingEmail | Contactable(decidedAs)`) becomes the
    single implementation of the Contactable Candidate rule. Messaging-summary gains an
    additive `contactability` field; the console maps server kind → the existing four
    buckets; the client-side classifier in `features/candidates/format.ts` is deleted
    (`contactBlockReason` stays as the label mapper). The dispatch audience and the
    report's exclusion buckets read the same code — exclusions in the send response are
    computed from the same Evaluate pass.
17. **One ticket, strict slice order.** Migration → `Features/Dispatches/SendToAll/`
    (handler + validator + seam tests) → `Features/Dispatches/RetryFailed/` → client
    feature. Retry is not independently shippable (no failed rows exist until Send runs);
    tests land per slice, not batched at the end.
18. **Mail stack: MailKit.** `IEmailSender` abstraction in Application `Abstractions/`,
    `MailKitEmailSender` in Infrastructure beside `PrivateFileStorage`, `SmtpOptions`
    bound from an `Smtp` config section beside the connection string. MailKit (latest
    stable, MimeKit's maintained companion) added to `Directory.Packages.props` per
    ADR-0012. Unconfigured SMTP (absent or effectively-empty section) refuses in the
    handler before any row is written: plain 400, stable code `Dispatch.SmtpNotConfigured`;
    the client keys its amber copy off the code.
    **Sub-decision (round 3, decision 28):** the Smtp section ships as a commented
    template in the deployed appsettings with provider examples (Gmail: host/port +
    app-password link), and the refusal copy names the concrete config file path —
    ADR-0019's "documented setup becomes a release requirement" lands here.
19. **Idempotency is enforced by the database.** Filtered unique index
    `UNIQUE ON dispatches(candidate_id) WHERE status = 'sent'` (EF `HasFilter`,
    hand-tuned migration). Audience filtering stays the primary path; a 23505 violation
    is caught per-candidate and counted as already-sent, never as a failure.
20. **Append-as-you-go; counts are derived, never stored.** `dispatch_runs` carries
    `started_at` / `completed_at` (null = interrupted — the crash state, for free); each
    `dispatches` row is written after its attempt with final status `sent | failed`,
    error text, and the rendered snapshot. No pending state, no stored counts, no rows
    for excluded candidates. Amends ADR-0019's "counts" wording (amendment recorded
    there same day).
21. **SMTP is faked at the boundary in seam tests.** `WebApplicationFactory` replaces
    `IEmailSender` (NSubstitute). Seam tests cover happy path, per-candidate failure
    continuation, unconfigured / missing-template / closed-vacancy refusals, and
    idempotent re-run; pack-mandated handler unit tests over `TestDbContext` carry the
    per-candidate matrix. First outbound-IO precedent at the seam — recorded explicitly.
    No real SMTP sink container unless a later wiring smoke test earns it.

## Grill decisions — 2026-09-20 (implementation grill, round 2: concurrency + client)

22. **Concurrent runs are blocked by a Postgres advisory lock.**
    `pg_advisory_xact_lock(hashtext('dispatch:' || vacancyId || ':' || roundId))` is the
    handler's first statement; a concurrent run fails fast with 400 +
    `Dispatch.RunInProgress`. The decision-19 unique index dedupes the *record*; the lock
    dedupes the *email* — both halves of "structurally impossible". Lock lifetime =
    request; a per-send SMTP timeout (~15 s) keeps the worst-case hold bounded.
23. **Client slice: new `src/features/dispatches/`, wired as a leaf of
    `useMessagingConsole`.** `api.ts` (`sendToAll`, `retryFailed` over `shared/http.ts`),
    `useSendToAll.ts` (view-state union `idle | confirming | sending | report` + refusal
    error), `format.ts` (report bucketing/copy), `components/`
    (`SendToAllConfirmDialog.vue`, `DispatchReportDialog.vue`). The Send To All button
    lives in the messaging console header beside the prepared-messages action — **not**
    inside `PreparedMessageListDialog` (that dialog *is* the mailto flow; decision 5's
    term boundary keeps the two mechanisms visually separate).
24. **Report dialog: one `UModal`, three regions.** Summary line ("N sent · M failed ·
    K excluded"), failed section (name + server error, mono, renders only when M > 0),
    excluded section (three groups, `contactBlockReason` labels, only when K > 0). Retry
    is one section-level "Retry failed (M)" button — the endpoint is run-scoped; per-row
    retry fakes a granularity the API doesn't have. Zero contactable candidates → the
    send button is **disabled with helper text**, not enabled-then-refused (the console's
    recipient list already names exclusions pre-send; amber refusal stays reserved for
    missing-template and unconfigured-SMTP, which are fixable configuration states).
25. **`lastSuccessfulDispatchAt` is a server-computed field** on the messaging-summary
    response (feeds decision 11's confirm copy; client never derives it by max-ing rows).
    After a run completes, the console **refetches messaging-summary** for the per-
    candidate `lastDispatch` indicators — the report dialog itself is fed by the send
    response, so one write path per state, no local merging.
26. **Layout doc written same session** (`01-server-dispatch.layout.md`): console header
    button states, already-notified confirm, in-flight spinner, report dialog regions,
    Dispatched indicator chip. Out of scope: prepared-messages dialog (decision 5),
    persistent dispatch history (decision 12).

## Grill decisions — 2026-09-20 (round 3: SMTP settings UX, reopening decision 6)

Context: a proposal to make SMTP "automatically configured from the users" / a settings
page challenged decision 6. The Velopack deployment research (2026-09-15) weakened the
"one-time IT task" framing — the installer may be the HR user themselves. The challenge
was grilled and partially accepted: the UX concern is real, but it splits into a small
docs win (this issue) and a new settings surface (its own issue).

27. **True auto-detection is dead.** OAuth2 (Gmail/Outlook) and app-specific passwords
    make "type your email address, we do the rest" impossible for a self-hosted tool —
    the credential step is provider-mandated user action. The achievable form is a
    provider-preset settings page (Gmail / Outlook / Custom → host+port auto-fill, user
    pastes an app password), the Gitea/GitLab/Discourse pattern.
28. **This ticket keeps file-based config; the settings page is its own issue**
    (`02-smtp-settings.md`, design settled same day). A settings page is a new user
    flow (one flow = one slice) carrying real weight — credentials in the domain DB
    (encryption-at-rest question), a test-connection endpoint, permission questions,
    the app's first settings surface — and doubling this ticket's scope was rejected.
29. **The amber refusal copy is keyed to which config path exists.** With issue 01 only:
    copy names the concrete config file path and points at the documented template.
    Once issue 02 lands, the copy gains a "Set up email sending" link to the settings
    route. Prepared Messages stays the no-SMTP fallback either way (decision 5).
30. **Settings-page governance (settles in issue 02, recorded here for ADR completeness):**
    any HR user may edit (no roles exist in the MVP to gate on), the form includes a
    Test connection handshake that must pass before save, last-write-wins on concurrent
    edits (consistent with the app's other edit surfaces).
31. **The account stays per-installation.** One shared account for the app instance;
    replies land in the shared HR mailbox. Per-user From addresses are an ADR-level
    pivot of their own, not part of either issue. ADR-0019's deferred-option wording
    amended same day to point at issue 02.

## Implementation notes — 2026-09-20

Deviations settled in code while implementing (branch `feat/implement-email-dispatch`):

- (a) Session-level `pg_try_advisory_lock` on a dedicated connection instead of
  `pg_advisory_xact_lock` on the write context — an xact lock contradicts
  append-as-you-go (a crash would roll back the idempotency records). Same key text
  (`dispatch:{vacancyId}:{roundId}`), same request lifetime.
- (b) Zero-audience / all-already-sent → 201 with `runId: null`; no run row is written
  without Dispatches.
- (c) Retry takes the same round lock and refuses a closed vacancy with
  `Dispatch.VacancyClosed` (409).
- (d) Retry skips candidates sent in any run and returns `excludedCount: 0` with empty
  `excluded` — the client keeps its prior exclusions from the send report.
- (e) Messaging-summary stays a bare array; the envelope for `lastSuccessfulDispatchAt`
  (decision 25) is deferred to the client slice so the console keeps working interim.
- (f) `contactability` / `lastDispatch.status` wire values are the client classifier's
  kebab-case kinds (`undecided|outcome-recorded|missing-email|contactable`, `sent|failed`).
- (g) `Dispatch → Candidate` FK is cascade: deleting a candidate removes its dispatch
  rows; run rows remain.
- (h) Contactability is computed for screened-out rows too — screening stays orthogonal
  (`screenedOut` flag); only the send path filters before classification.
- (i) `dotnet ef migrations add` regenerates `AppDbContextModelSnapshot` and drops the
  hand-maintained `internal static BuildSnapshotModel(ModelBuilder)` helper that two old
  Designer files (`20260901120000_AddPendingFileDeletion`,
  `20260910070000_Add_Candidate_Promotion_Provenance`) call. Re-add it after each
  scaffold (`BuildModel` delegating to it), or migrate the old Designers to the standard
  pattern. A code comment at the helper says the same.
- (j) `TestDbContext` (SQLite-free fake over the relational model) configures Dispatch
  tables without the filtered unique index `dispatch_candidate_sent_key`; the index is
  integration-covered by the real migration and the 23505 path is unit-covered via the
  `UniqueViolationOnceDbContext` fake.
- (k) Bug found by the new handler tests: `SendToAllCommandHandler` must
  `.Include(c => c.FormResponses)` when loading the round's candidates —
  `Candidate.EvaluateScreening` reads the current form response, and without the include
  no rule fires and screened-out candidates would be emailed.

