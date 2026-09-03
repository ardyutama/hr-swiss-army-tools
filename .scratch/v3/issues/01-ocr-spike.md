# 01: OCR spike

**What to build:** A throwaway spike in `.scratch/ocr-spike/` answering three questions
before the V3 extraction slice is committed:

1. **.NET compatibility** — does Sdcb.PaddleOCR run on the project's target framework
   (.NET 10) in this repo's hosting setup?
2. **Accuracy** — run it on 5–10 of the worst real (anonymized) PDFs: design-heavy,
   scanned, multi-column. Is the extracted text good enough for word-boundary phrase
   matching of vacancy requirements?
3. **Latency** — per-page OCR time; is an interval background worker (ADR-0006 sweeper
   pattern) fast enough for import-time extraction?

Verdict outcomes: binding works → in-process OCR (ADR-0009 as written); binding fails →
spike a Python sidecar; local accuracy unacceptable → reconsider cloud OCR. Record the
verdict on ADR-0009 (proposed → accepted or amended), then write the V3 slice spec.

**Blocked by:** Nothing technical, but do not start without the user's explicit go.

**Status:** needs-triage

- [ ] Sdcb.PaddleOCR runs on target framework
- [ ] Accuracy acceptable on worst real PDFs
- [ ] Per-page latency measured; worker approach validated
- [ ] Verdict recorded on ADR-0009
