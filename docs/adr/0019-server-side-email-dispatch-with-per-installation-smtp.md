# ADR-0019: Server-side email dispatch with per-installation SMTP

## Status

Accepted (2026-09-20, grill-with-docs session; design only, implementation follows —
15 decisions recorded in `.scratch/send-to-all/issues/01-server-dispatch.md`)

## Context

US-19 asks for one template per vacancy "sent to all in one action." The MVP delivered
half of it: the messaging console renders templates per candidate and opens a `mailto:`
link (or clipboard fallback) so HR sends from their own email client — a Prepared
Message, per the glossary. At round scale (tens of rejection emails) that model is a tab
storm against popup blockers, and "minutes not hours" dies. The obvious next step —
automating the mailto loop client-side — was considered and rejected: it keeps the tab
storm, breaks silently under browser popup policies, and still can't answer "did this
person actually get mailed?"

Sending real mail server-side forces a harder question: this product ships self-hosted
on-prem (Windows desktop installs per the 2026-09-15 research), so there is no vendor
mail service to hide behind. Every customer's mail must flow through an account they
control.

## Decision

Send To All is a **server-side dispatch** over a **per-installation SMTP account**,
governed by the Contactable Candidate rule:

1. **Mechanism.** `POST /api/vacancies/{vacancyId}/rounds/{roundId}/dispatches` derives
   the audience itself (empty body): the round's Contactable Candidates — Rejected and
   Bench members with a contact email — with Screened Out candidates filtered out before
   classification. The server renders each candidate's template kind (existing render
   endpoint semantics) and sends sequentially over SMTP, in-request; one failure never
   aborts the run. A missing template kind blocks the whole run with an amber refusal.
2. **SMTP is installation configuration, not domain data.** Host, credentials, and sender
   live in appsettings/environment beside the connection string; there is no settings UI
   and no credentials in the domain DB. Unconfigured SMTP keeps the action visible but
   refuses with amber, actionable copy — never hidden, never a silent mailto fallback.
   Mail reputation and deliverability belong to the customer's account, deliberately.
3. **Every send is recorded, and success is never repeated.** Two tables: `dispatch_runs`
   (the batch: vacancy, round, timestamps, counts) and `dispatches` (per candidate:
   template kind, rendered subject/body **snapshot**, sent/failed, error, timestamps).
   The snapshot survives later template edits — "what exactly did we send this person?"
   stays answerable. Idempotency is per Candidate row: retry goes through
   `POST …/dispatches/{runId}/retry` and only ever targets failed rows, so a double-send
   is structurally impossible. A new Candidate row (promoted, re-imported) is a fresh
   start — same person, different candidate, per the glossary.
4. **One round, viewed, vacancy open.** The audience is one Intake Round (ADR-0010),
   never cross-round — whole-vacancy sends would double-mail people who exist as a
   different candidate per round. Dispatching into a *closed* round is legal (settled
   review data is never touched; dispatch writes its own records), matching the existing
   mailto behavior; a closed *vacancy* refuses as a Lifecycle Conflict.

The mailto/clipboard Prepared Message flow stays untouched as the no-SMTP path and the
per-candidate path. Glossary impact: **Dispatch** and **Dispatch Run** added, and
**Contactable Candidate** extended to cover both flows (CONTEXT.md, 2026-09-20).

## Considered options

- **Client-automated mailto loop** — rejected: popup-blocker fragility, no record of
  what went out, not "automatically" in any sense HR would recognize.
- **Background job with polling** — rejected: no job infrastructure exists; at tens of
  emails per run, sequential-in-request is honest and debuggable. The schema leaves the
  door open if scale ever demands it.
- **DB-backed SMTP settings with an in-app settings page** — deferred to follow-up
  issue `.scratch/send-to-all/issues/02-smtp-settings.md` (provider-preset form +
  test-connection gate, design settled 2026-09-20, round 3 of the same grill): the
  send code reads an options abstraction either way, so the UI can come later without
  rework. The interim path ships as a commented config template with provider examples,
  and the refusal copy names the concrete file path.
- **Cross-round / whole-vacancy audience** — rejected: violates round ownership and
  double-mails repeat applicants; promotion is the deliberate path for reaching last
  round's people.

## Consequences

- **Onboarding burden:** an install without SMTP settings cannot dispatch. The amber
  refusal copy must name the fix (whoever installed the app edits the config file and
  restarts). Documented setup becomes a release requirement.
- **Deliverability is the customer's:** bulk near-identical emails from a fresh SMTP
  account can trip spam filters. This is accepted by design — the alternative (a
  vendor relay) contradicts self-hosted on-prem.
- **Dispatches outlive templates:** the rendered snapshot is the audit record, so
  editing or deleting a template never rewrites history — and never blocks it either.

## Amendment — 2026-09-20 (implementation grill, decision 20)

`dispatch_runs` stores **no counts**: the report derives them by joining run →
dispatches, because a stored count is a cache a mid-run crash corrupts.
`completed_at IS NULL` is the interrupted-run state; recovery is point 3's idempotency
rule, unchanged.
