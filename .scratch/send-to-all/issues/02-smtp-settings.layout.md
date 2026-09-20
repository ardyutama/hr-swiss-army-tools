# 02-smtp-settings — layout

Companion UI shape doc for `02-smtp-settings.md` (decisions 1–16). Pins the settings
page in ADR-0008 language: light-only warm-utilitarian, Geist, one accent (primary
`#4361a8`), status colors semantic-only, 12px radius on cards/inputs, full-pill on
badges/buttons, working-dense (5–6), restrained motion, state language per decision 14,
no dead UI per decision 15.

Design read: dense internal productivity tool, not a marketing surface — landing-page
rules from the design-taste skill do not apply (ADR-0008 context). Full state cycles,
plain copy, inline errors, label-above-input form pattern, and WCAG AA contrast do.

---

## A. Sidebar navigation (ADR-0008 decision 15)

The sidebar gains a second label group. No dead UI: the group renders only when the
settings page exists (this slice).

```
┌─ Sidebar ─────────────────────────┐
│  Sorting CV                       │
│  ▸ Vacancies        (active)      │
│                                   │
│  Settings                         │
│  ▸ Email sending                  │
└───────────────────────────────────┘
```

- Label: **Settings** (same `px-3 font-bold uppercase tracking-[0.1em]
  text-sidebar-foreground/60` style as "Sorting CV").
- Item: **Email sending** → `/settings/email`. Active state follows the same rule as
  Vacancies (explicit `active` binding on `route.path`).
- No gear icon; no header chrome added.

---

## B. Page header

```
┌─ Content ─────────────────────────────────────────────────────────────┐
│  Email sending                                                        │
│  Set the account this installation uses to send email.               │
│                                                                       │
│  Sending as hr@firma.example via smtp.gmail.com:587 — saved here.    │
└───────────────────────────────────────────────────────────────────────┘
```

- H1: `Email sending` (page title).
- Subtitle: one sentence, `text-sm text-ink/60`, max-width 65ch.
- **Effective-source status line** (decision 10): muted, one line, three states:
  - `Sending as hr@firma.example via smtp.gmail.com:587 — saved on this page.`
  - `Sending as hr@firma.example via smtp.gmail.com:587 — from the configuration file.`
  - `Email sending is not configured.` (neutral, not amber — this is the default
    empty state, not a refusal).
- The status line updates in place after save/remove; no toast.

---

## C. Form card (one card, 12px radius, max-w-lg)

```
┌─────────────────────────────────────────────┐
│  Provider                                   │
│  ┌─────────┐ ┌─────────┐ ┌─────────┐       │
│  │ Gmail   │ │ Outlook │ │ Custom  │       │
│  │ smtp…   │ │ smtp…   │ │ Any SMTP│       │
│  │ :587    │ │ :587    │ │ host    │       │
│  └─────────┘ └─────────┘ └─────────┘       │
│  Requires 2FA → create an app password      │
│                                             │
│  Host *                                     │
│  [ smtp.gmail.com            ]              │
│  Port *                                     │
│  [ 587                       ]              │
│                                             │
│  Username *                                 │
│  [ hr@firma.example          ]              │
│  Password *                                 │
│  [ Saved — type to replace   ]              │
│  App passwords are provider-specific.       │
│                                             │
│  From address *                             │
│  [ hr@firma.example          ]              │
│  From name                                  │
│  [ HR Team                     ]            │
│                                             │
│  [ Test connection ]  [ Save ]  (Save       │
│  disabled until test passes)                │
│                                             │
│  ┌─ Test result (inline) ─────────────┐    │
│  │ Testing… / Connected and           │    │
│  │ authenticated. / Authentication    │    │
│  │ failed — check the app password.   │    │
│  └────────────────────────────────────┘    │
│                                             │
│  Remove saved settings  (text, error color,  │
│  only when a saved row exists)              │
└─────────────────────────────────────────────┘
```

- **Preset selector** (decision 5/15): `URadioGroup` with three cards. Helper text per
  card under the group: Gmail "Requires 2FA → create an app password"; Outlook "Create
  an app password in account security"; Custom "Any SMTP host". Selecting a preset
  fills host/port and resets test-passed state.
- **Inputs** (decision 6): label above, helper text optional, error below, `gap-2`.
  All fields required except From name.
- **Password** (decision 6): write-only. When `hasPassword` is true the field shows
  the placeholder "Saved — type to replace"; submitting sends the new value only when
  the user typed one.
- **Actions row**: Test connection (secondary) → Save (primary). Save is disabled
  until the current form values have passed test (decision 3/30). After a successful
  test, any edit returns the form to must-test-again.
- **Test result**: inline status block under the actions, four states (idle / testing /
  success / failure with the server's sanitized message). Success uses success color
  `#1c7c43`; failure uses error `#c94f4f`; never toast.
- **Remove saved settings** (decision 11): quiet text action, error color, rendered
  only when `source === 'settings'`. Confirm dialog: "Remove the saved SMTP settings?
  The app will fall back to the configuration file if it exists."

---

## D. Empty state (source = 'none')

The page still renders the full form; the status line reads "Email sending is not
configured." and the password field placeholder is "Enter app password". No special
empty-state component — the form is the content.

---

## E. Error and edge states

- **Validation errors** (client + server): inline under each field, error color, one
  sentence per field. Host: "Enter a hostname or IP address." Port: "Enter a port
  between 1 and 65535." Username: "Enter the account username." From address:
  "Enter a valid email address."
- **Test failure** (`EmailSettings.TestFailed`): inline block with the server's
  sanitized message; the Save button stays disabled.
- **Save failure** (network/500): inline block under the actions: "Could not save —
  try again."; the form values stay.
- **Remove confirm**: `UModal` 12px radius, Cancel + Remove (error color). After
  removal the status line flips to "from the configuration file" or "not configured".

---

## F. Sidebar active state and route

- Route: `/settings/email` → `SettingsEmailView.vue`.
- Router entry added to `src/router.ts`; sidebar item added to `App.vue` under a
  second `UNavigationMenu` group labeled "Settings".
- Deep-linkable; browser back returns to the previous vacancy context.

---

## G. Copy self-audit

- No "Oops", no apology theater, no exclamation marks.
- "Test connection" not "Verify" — the button tests the connection, not the account.
- "Saved — type to replace" not "••••••••" — dots imply a retrievable value.
- "Set up email sending" (banner link) not "Configure SMTP" — the banner speaks HR,
  the page speaks IT; the link bridges them.
