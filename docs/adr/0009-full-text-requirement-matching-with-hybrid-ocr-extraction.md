---
status: proposed
---

# Full-text requirement matching with hybrid OCR extraction (V3)

Decided 2026-09-03, implementation deferred to V3 — V1 ships the review workspace and hire
tracking without any parsing.

Requirement Match is a case-insensitive word-boundary phrase match of each vacancy
requirement against the primary CV's full extracted text, with an explicit token rule for
"C#" / ".NET"; fuzzy matching and stemming are banned. The Candidate Skill concept is
deleted: there is no skill extraction, only full text.

Extraction is hybrid: PdfPig as the fast path with a per-page OCR fallback when a page's
text layer is too thin. OCR runs in-process via a .NET binding (Sdcb.PaddleOCR); a Python
sidecar is the fallback if the spike fails, cloud OCR only if local accuracy is
unacceptable. The pipeline is async: import writes pending-extraction rows in-transaction
and an interval worker (the ADR-0006 sweeper pattern) extracts and flips Extraction Status;
extracted text lives in the database on the CV Document. Matching runs in-memory in C# —
no tsvector, no ILIKE.

## Consequences

- The exact-match "Candidate Skill ↔ Vacancy Requirement" model from the MVP spec is gone;
  extraction failures degrade to empty candidate details with manual editing as the UX.
- A spike in `.scratch/v3/issues/01-ocr-spike.md` gates the OCR-binding leg of this
  decision; the verdict moves this ADR to accepted or amends it.
