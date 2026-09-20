# 01-server-dispatch — layout

Companion UI shape doc for `01-server-dispatch.md` (decision 26). Pins the messaging
console's Send To All surfaces in ADR-0008 language: light-only warm-utilitarian, Geist,
one accent (primary `#4361a8`) per screen, status colors semantic-only, 12px radius on
dialogs, full-pill badges/buttons, working-dense (5–6), restrained motion, state language
per decision 14, no dead UI per decision 15.

Design read: dense internal productivity tool, not a marketing surface — landing-page
rules from the design-taste skill do not apply (ADR-0008 context). Full state cycles,
plain copy, and empty-state discipline do.

---

## A. Messaging console header — two distinct send actions

The console header currently has one chrome button, "Send To All", which opens the
prepared-messages (mailto) dialog. After this slice the header carries **two** actions.
The term boundary (decision 5: Prepared Message vs Dispatch) must be legible in the
chrome itself:

```
┌─ Messaging ────────────────────────────────────────────────────────────────┐
│  Round: 2026 intake ▾            [ ✉ Send to all ]  [ 📋 Prepared messages ]│
└─────────────────────────────────────────────────────────────────────────────┘
```

- **✉ Send to all** — the server dispatch action. **Primary button** (accent fill,
  full-pill). Label is the glossary verb phrase; sentence case per existing chrome.
  - **States**: `ready` → `disabled` (zero contactable candidates, helper text below —
    section E) → `busy` (run in flight, section D). The **missing-template** and
    **unconfigured-SMTP** states are *not* pre-computed in the chrome — the button stays
    ready and the click refuses amber (section C); the console already shows template
    coverage and IT owns SMTP config. Only the zero-audience state is a disabled button,
    because its cause is visible data, not configuration.
  - When the round has any successful Dispatch, the click first raises the confirm
    (section B).
- **📋 Prepared messages** — the existing mailto entry point, relabeled from
  "Send To All". **Secondary/ghost button**, same pill shape. Behavior untouched
  (decision 5): opens `PreparedMessageListDialog`. The rename is the smallest change that
  keeps the two mechanisms from sharing one verb.

Icon rule per ADR-0008 scope: one icon family as the app already uses; no new icon
library. If the header grows crowded at narrow widths, the prepared-messages button
collapses to icon+tooltip first; the primary action never loses its label.

---

## B. Re-notify confirm dialog (`SendToAllConfirmDialog.vue`)

Shown only when the summary's `lastSuccessfulDispatchAt` is non-null (decisions 11, 25).
Reuses the `OutcomeConfirmDialog` pattern: `v-model:open`, `busy`, `error`, `@confirm`.

```
┌──────────────────────────────────────────────────┐
│  Send to all contactable candidates?             │
│                                                  │
│  This round was already notified on              │
│  18 Sep 2026. Candidates who already received    │
│  the email will not be contacted again.          │
│                                                  │
│  Sending goes to 17 contactable candidates.      │
│                                                  │
│              [ Cancel ]  [ Send to 17 ]          │
└──────────────────────────────────────────────────┘
```

- Copy names the prior date and states the idempotency rule in plain language — the
  confirm is reassurance, not a warning. Neutral tone, **no amber**: this is a normal
  re-run path, not a safety net (amber stays reserved per repo convention).
- Confirm button label carries the count (decision-24 summary source: the summary's
  contactable count). One primary CTA, one cancel — no duplicate intent.
- While `busy`, buttons disable and the confirm shows its spinner state (section D
  takes over after the request lands).

---

## C. Amber refusal — unconfigured SMTP and missing template kind

Per decisions 4, 7, 18: refusal is a 400 ProblemDetails with a stable code
(`Dispatch.SmtpNotConfigured`, `Dispatch.MissingTemplate`); the client keys copy off the
code. Rendered **inline in the console**, not toast-only (ADR-0008 decision 14: "errors
render inline in the region that failed, never toast-only"). Amber `#b45309` is the
established warning token (ADR-0008 decision 2 amendment; safety-net warnings are the
sanctioned use beyond Flagged).

```
┌─ Messaging ────────────────────────────────────────────────────────────────┐
│  ⚠ Email sending is not configured. Ask whoever installed this app to set  │
│    the SMTP account in the configuration file and restart it. You can      │
│    still send with Prepared messages.                                      │
└─────────────────────────────────────────────────────────────────────────────┘

┌─ Messaging ────────────────────────────────────────────────────────────────┐
│  ⚠ No rejection email template yet. Create it under Templates before       │
│    sending to all — no emails were sent.                                   │
└─────────────────────────────────────────────────────────────────────────────┘
```

- Copy is actionable and names the fix owner ("whoever installed this app" — the
  installer/IT, per ADR-0019's onboarding consequence). The mailto path is pointed at,
  never auto-invoked (decision 7: no silent fallback).
- Missing-template copy names the kind ("rejection" / "shortlist") from the refusal
  payload's extension. Both refusals write **no run row** (decision 18/20).
- Dismissible; re-appears on the next click while the cause persists. No dead UI:
  the banner is absent in healthy states.

## D. Run in flight

The click path: confirm (if needed) → request in flight. Duration is seconds-to-minutes
(sequential sends, ~15 s per-send worst case, decision 22). ADR-0008 decision 14: "a
plain centered loading label for fast operations" — this operation is *not* fast, so the
in-flight state is a **blocking modal** (the report dialog's own `sending` state), not a
spinner on the button:

```
┌──────────────────────────────────────────────────┐
│  Sending emails…                                 │
│                                                  │
│  ⏳  Contacting your SMTP account and sending     │
│     one email at a time. This can take a few     │
│     minutes for large rounds.                    │
│                                                  │
│  You can close this dialog — sending continues   │
│  and the recipient list updates when it finishes.│
└──────────────────────────────────────────────────┘
```

- Honest copy about duration (sequential is a settled architecture decision, 8/ADR-0019;
  the UI must not pretend otherwise).
- Closeable but non-cancellable: closing dismisses the dialog; the run continues
  server-side (cancelling HTTP mid-run would strand the lock's parent request anyway).
  On completion, messaging-summary refetches (decision 25) and indicators update behind
  the closed dialog.
- If the dialog is open when the response lands, it transitions in place to the report
  (section F). A failed-to-start refusal (section C) replaces the spinner with the
  inline error in the same dialog, never a dead-end.

## E. Zero-audience disabled state

Zero contactable candidates → the primary button is disabled, helper text directly
under the header actions (decision 24):

```
[ ✉ Send to all ]  (disabled, 50% opacity)
No contactable candidates in this round — the list below shows why each was excluded.
```

The recipient list already carries per-candidate contactability reasons
(`contactBlockReason`); the helper text points at it instead of duplicating the buckets.

## F. Report dialog (`DispatchReportDialog.vue`) — three regions, one scroll

Decision 24: summary → failed (if any) → excluded (if any). One `UModal`, 12px radius,
no tabs/accordion.

```
┌──────────────────────────────────────────────────────────────┐
│  Send complete                                               │
│                                                              │
│  23 sent · 2 failed · 5 excluded                             │
│                                                              │
│  ── Failed ──────────────────────────────────────────────    │
│  J. Novak        SMTP timeout after 15s                      │
│  A. Diallo       Recipient address rejected: mailbox full    │
│                              [ Retry failed (2) ]            │
│                                                              │
│  ── Excluded ────────────────────────────────────────────    │
│  Not yet shortlisted or rejected (3):  K. Vos, M. Ito, …     │
│  Hire outcome already recorded (1):    R. Prins              │
│  Needs an email address (1):           T. Okoro              │
│                                                              │
│                                   [ Done ]                   │
└──────────────────────────────────────────────────────────────┘
```

- **Summary line**: counts as inline text with `·` separators, tabular numbers (mono
  allowed for data density per ADR-0008 decision 1). Sent = success color `#1c7c43`;
  failed = error `#c94f4f`; excluded = ink at reduced emphasis. Colors are semantic
  status, not decoration.
- **Failed rows**: candidate name + server error verbatim in mono — the error is
  operational detail for IT, not marketing copy. **One** section-level retry button
  (run-scoped endpoint, decision 13); per-row buttons banned (fakes granularity the API
  doesn't have). Retry response refreshes the counts in place; a fully-recovered section
  collapses and the summary line updates.
- **Excluded groups**: three buckets, labels from `contactBlockReason` (single label
  source — decision 16 keeps classification server-side, labels stay client display).
  Screened Out never appears (decision 14).
- **Copy self-audit**: no apology theater, no "Oops", no exclamation marks. "Send
  complete" even with failures — the run completing is the fact; failures are data below.
- Retry-all-failed and Done are distinct intents; no third CTA.

## G. Per-candidate Dispatched indicator (console recipient list)

Decision 15: display-only, never blocks, same posture as Prior Application Notice /
Resubmitted.

```
K. Vos      kv@example.com      [Rejected]  ·  Dispatched 18 Sep
```

- Plain text with `·` separator after the status badge, **not** a colored pill — the
  badge family stays reserved for review state. Muted ink, same size as the existing
  meta text. Failed dispatch shows `Dispatch failed` in error color (it's the one
  actionable variant). Absent when never dispatched (`lastDispatch: null`).
- Sort/filter by dispatch state is **not** in this slice — history surface is deferred
  (decision 12); the indicator answers "did this person get it?" at a glance and nothing
  more.

## Explicitly out of scope (decision 26)

- `PreparedMessageListDialog` internals — untouched (decision 5).
- Persistent dispatch-history page — deferred (decision 12); the schema supports it.
- Settings UI for SMTP — never; config file (decision 6).
- Dark mode, motion beyond existing transitions, any new accent color.
