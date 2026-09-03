# ADR 0009: Full-text requirement matching with hybrid PdfPig/OCR extraction

## Status

Proposed (2026-09-03). The OCR-hosting leg (in-process .NET binding) is conditional on
the `.scratch/ocr-spike/` verdict; a spike failure escalates to a Python sidecar, then
to a cloud OCR API, and this ADR is amended accordingly.

## Context

Real candidate CVs break the import-time pipeline. PDFs are 8–10 pages on average,
the CV content may start on any page, and non-ATS designer layouts defeat the
line-oriented `CandidateTextParser`: even after rebuilding visual lines from word
coordinates, skill extraction produced 0/3 matches on real documents. The glossary's
Requirement Match ran against *parsed Candidate Skills*, so parser fragility became
matching failure — the feature HR actually uses. Separately, image-only and scanned
pages yield no text layer for PdfPig at all, so some CVs can never be matched no
matter how good the parser gets.

## Decision

- **Requirement Match targets full extracted text, not parsed skills.** A requirement
  matches when it appears in the primary CV's Extracted Text under case-insensitive,
  trim-normalized, word-boundary phrase comparison, with an explicit token rule for
  punctuation-bearing tokens ("C#", ".NET"). Fuzzy matching and stemming are
  deliberately rejected (glossary `_Avoid_`), as is Postgres `tsvector`, whose lexeme
  tokenization destroys exactly those tokens.
- **Candidate Skill extraction is deleted.** With matching on full text, the parser's
  skill extraction loses its only consumer; the concept leaves CONTEXT.md and the
  code. Candidate Details extraction (name/contact from anywhere in the text)
  survives — it is easier on raw text, not harder.
- **Extracted Text is persisted on the CV Document in the database** at extraction
  time. Matching runs in-memory in C# over the stored text (candidate counts per
  vacancy do not justify a search index, and `ILIKE` would push word-boundary
  semantics into SQL). No sidecar `.txt` files: disk/DB drift is what ADR-0006's
  sweeper exists to kill.
- **Extraction is hybrid: PdfPig fast path, per-page OCR fallback.** Most CVs are
  born-digital; OCR of a good text layer is slower and *worse*. A page whose PdfPig
  text yield falls below a thinness threshold (tuned on real worst-case samples during
  the spike) is rasterized and OCR'd; page texts are merged. OCR-everything was
  rejected: the worst case must not tax the common case.
- **OCR runs in-process via a .NET PaddleOCR binding** (e.g. Sdcb.PaddleOCR) behind a
  new `IOcrTextExtractor` abstraction beside `IPdfTextExtractor`. A Python sidecar
  (canonical PaddleOCR) is the first escalation — it adds a second language and
  deployable; a cloud OCR API is the last — per-page cost, latency, and candidate
  personal data leaving the machine conflict with the private-file posture of
  ADR-0006.
- **All extraction moves off the import request.** Import stores the PDF and writes a
  pending-extraction record in the same transaction; an interval worker (the ADR-0006
  sweeper pattern: status rows + `BackgroundService`) performs PdfPig→OCR extraction
  and flips Extraction Status. Inline extraction was rejected because OCR at 8–10
  pages is seconds-to-tens-of-seconds per CV, and bulk imports would block for
  minutes. An in-process Channel queue was rejected: it loses work on crash unless
  status rows are written first anyway.
- **OCR failure is final and manual.** An unreadable document sets Extraction Status:
  failed; HR edits Candidate Details by hand in the existing review UI. No automatic
  retry (OCR of garbage is expensive garbage); a manual retry action is a follow-up
  ticket, not part of this decision.

## Consequences

- Import becomes fast and uniform; extraction results are **eventual**, surfaced
  through Extraction Status — the same convergence vocabulary ADR-0006 established
  for file deletion.
- `CandidateTextParser` shrinks to contact-details extraction; the line-reconstruction
  workaround in `PdfPigTextExtractor` remains load-bearing for details, but matching
  no longer depends on line structure at all — page-order and layout inconsistency
  stop mattering.
- Matching quality is bounded by OCR quality on image-only pages; the spike exists to
  measure that bound before the slice ships.
- The thinness threshold and per-page latency are configuration, tuned from spike
  measurements rather than guessed.
