# V3 Spec — CV Text Extraction and Requirement Match

Deferred from the MVP on 2026-09-03: V1 ships the workspace and tracking loop without any
parsing; this iteration adds the extraction pipeline on top. The design below was grilled
and settled 2026-09-03 and is recorded as ADR-0009 (proposed).

## Settled design (ADR-0009)

- **Requirement Match**: case-insensitive word-boundary phrase match of each vacancy
  requirement against the primary CV's full extracted text; explicit token rule for
  "C#" / ".NET"; fuzzy/stemming banned. "Candidate Skill" as a concept is deleted —
  there is no skill extraction.
- **Hybrid extraction**: PdfPig fast path; per-page OCR fallback when a page's text layer
  is too thin (threshold tuned on the worst real PDFs).
- **OCR hosting**: in-process .NET binding (Sdcb.PaddleOCR) in new repo folders; Python
  sidecar only if the spike fails; cloud only if local accuracy is unacceptable.
- **Async pipeline**: import writes pending-extraction rows in-transaction; an interval
  worker (ADR-0006 sweeper pattern) extracts and flips Extraction Status. Extracted Text is
  stored in the DB on the CV Document (no sidecar files).
- **Matching** runs in-memory in C# (no tsvector, no ILIKE).
- **Failure UX**: candidate stays listed with fields empty; HR edits details manually
  (V1 ticket 08-edit-candidate-details already provides the screen); retry = follow-up
  ticket.

## Sequence

1. **Spike** — [.scratch/v3/issues/01-ocr-spike.md](issues/01-ocr-spike.md). Not started;
   waiting for the user's go.
2. **Slice** — spec written after the spike returns a verdict: extraction worker,
   Candidate Details auto-fill (name/email/phone) replacing manual-only entry, Match column
   restored in the candidate list, requirements panel in the review workspace becomes
   computed matches.

## Relation to V1

- V1 candidate identity = source sender fallback + manual entry; V3 auto-fills the same
  Candidate Details fields from the primary CV instead.
- V1 keeps vacancy requirements as a manual checklist in the review workspace; V3 computes
  the Match from them.
