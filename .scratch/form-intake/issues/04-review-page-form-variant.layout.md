# 04 layout proposal: intake-source identification + review form variant

Design read: dense internal triage tool, not a marketing surface — the design-taste
skill's landing-page rules are out of scope (its own Section 13; ADR-0008 §16). What
binds: warm-utilitarian locked palette, light-only, Geist, radius 12px, one accent
(`--ui-primary`), density 5–6, motion 3–4, Nuxt UI v4 components only, semantic tables,
shape-matched skeletons, inline errors, no dead UI, amber reserved for Flagged /
Lifecycle Conflict (safety-net warnings excepted).

Companion to [04-review-page-form-variant.md](04-review-page-form-variant.md) — pins the
shape of grill decisions 1–6 (2026-09-20).

## Candidate list (decisions 1–4)

Six columns, unchanged: Candidate | Received | CV | Notes | Review status | Actions.

**Candidate cell.** A muted `UIcon` precedes the display name, tooltip names the source:

```
┌──────────────────────────────────────┬──────────────┬──────────────┐
│ Candidate                            │ Received     │ CV           │
├──────────────────────────────────────┼──────────────┼──────────────┤
│ ✉ Budi Santoso                       │ Sep 12, 9:41 │ 📎           │  email + PDF
│   (chips…)                           │              │              │
│ ✉ Anon — re: operator                │ Sep 12, 8:03 │ [No CV]      │  email, no PDF (amber badge)
│ ▤ Siti Rahma                         │ Sep 12, 9:58 │ ↗ Link       │  form + link (clickable)
│ ▤ Joko (resubmitted)                 │ Sep 14, 7:12 │ [No CV link] │  form, empty link (amber badge)
└──────────────────────────────────────┴──────────────┴──────────────┘
```

Icons: `i-lucide-mail` (Source Email), `i-lucide-file-text` (Form Response) — `text-muted`,
`size-4`, vertically centered with the name line, `title` = "Source Email" / "Form Response"
for the tooltip; the `sr-only` row text names the source too (screen readers get the glyph's
meaning, not silence).

**CV cell state machine** (decision 1):

| Intake source | Evidence | Cell | Color |
|---|---|---|---|
| email | `cvDocumentCount > 0` | `i-lucide-paperclip` | muted (unchanged) |
| email | `cvDocumentCount = 0` | badge "No CV", `i-lucide-file-warning` | warning/amber (unchanged) |
| form | `cvLink` non-empty | `i-lucide-external-link` + "Link" | neutral, `variant="link"`, opens URL in new tab, `@click.stop` |
| form | `cvLink` empty/null | badge "No CV link", `i-lucide-file-warning` | warning/amber |

The clickable Link is a `UButton variant="link"` (or `ULink`) with `target="_blank"
rel="noopener"`, ≥40px hit width is not required inside a dense row but the badge states
stay pill-shaped per the shape lock. Link presence derives from the new `cvLink` field on
`CandidateSummaryResponse` (decision 8); the frontend never parses cells itself.

**Received cell** (decision 4): form rows render their form Timestamp through the same
`formatReceivedAt`; the reader coalesces the sort key, so clicking the Received sort never
disagrees with the displayed dates.

## Review workspace (decisions 5–6)

### Header meta line (both variants)

Second line of `ReviewHeader`, muted `text-sm`, middle-dot separated (≤1 dot per adjacent
pair, per the ration):

```
Budi Santoso                              3 of 47 · 12 processed
Form Response · Submitted Sep 12, 9:58 · [Resubmitted]        [↗ Open CV  C ]
```

- **Source pill**: subtle neutral badge, "Form Response" or "Source Email".
- **Form timestamp**: form variant only, "Submitted <formatted>" from
  `formResponses[current].formTimestampParsed`; raw string fallback if unparsed. System
  data — never a picked column, per ADR-0013 §4.
- **Resubmitted badge**: when `isResubmitted`, neutral badge (matches the list's existing
  badge), copy "Resubmitted", tooltip "A newer form response replaced the stored one on a
  later upload. Review status, notes, and typed details were kept."
- **Open CV button**: form variant only, right-aligned in the header; disabled + label
  "No CV link" (never hidden — HR must see the gap) when the link cell is empty. `C`
  keyboard shortcut fires the same action; the shortcut is listed in the `?` help modal.
  `C` is inert on the email variant.

### Form variant grid

```
┌────────────────────────────┬─────────────────────────────────┐
│ Requirements (pinned)      │ Form Answers                    │
│ Candidate Details          │  1 · Position applied           │
│ Prior Application Notice   │     Operator produksi           │
│ Notes (always visible)     │  5 · Experience                 │
│                            │     3 years, PT X …             │
│  (~40–45%)                 │  (picked columns, column order, │
│                            │   read-only, ~55–60%)           │
└────────────────────────────┴─────────────────────────────────┘
```

- **FormAnswersPanel** replaces `CvViewer` in the sticky right slot. Rows are
  `label → value` pairs in column order; label = Column Label if set, else the header
  snapshot (truncated), with the other shown muted beside it per the layout panel's
  verifiability rule. Multi-line cell text wraps; long URLs break. Read-only — no inputs.
- **CvViewer is email-only.** Form variant never mounts it (no dead empty pane).
- **SourceEmailPanel is email-only** (already effectively so: form candidates have no
  source subject/body); the form variant drops it from the left column explicitly.
- **Left column unchanged otherwise**: Requirements, Candidate Details (pre-filled by
  layout roles), Prior Application Notice, Notes. Action bar (S/F/R, ←/→, outcomes)
  identical in both variants — decision shortcuts must not depend on evidence type.
- **States**: FormAnswersPanel reuses the CvViewer grammar — shape-matched skeleton while
  loading, inline error region on failure, and an empty state ("This response has no
  picked columns to show." — one sentence, no action) if the layout picks nothing
  displayable. Email candidate with zero PDFs keeps the CvViewer empty state (decision 6).
