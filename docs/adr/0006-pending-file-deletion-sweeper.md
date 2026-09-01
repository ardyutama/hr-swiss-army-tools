# ADR 0006: File-storage cleanup via pending-deletion records and a background sweeper

## Status

Accepted (2026-09-01). Amends the post-commit file-deletion behavior implied by
ADR-0004/0005 handlers (`DeleteCandidate`, `PurgeVacancy`, `ImportCandidates`).

## Context

The database and the file system cannot be updated atomically. The delete/purge
handlers removed files **after** the database transaction committed; the import
handler removed files in a catch-block compensation **before** the transaction
resolved. Both directions could orphan files (process death between the two writes,
or a delete failure mid-loop), and `PrivateFileStorage.DeleteAsync` swallowed
`IOException`/`UnauthorizedAccessException`, so failures were invisible — an HTTP 200
could be returned while a CV remained on disk.

Domain events were considered and rejected: the delete/purge handlers use
`ExecuteDeleteAsync` and never call `SaveChangesAsync`, so raised events would be
silently dropped; and `AppDbContext` dispatches events post-SaveChanges but still
inside the explicit transaction, which would run a file-deleting event handler
**pre-commit** and flip the orphan direction (row rolled back, file already gone).
Only an out-of-transaction mechanism survives process death and retries.

## Decision

- Delete and purge handlers no longer touch storage. They write one
  `PendingFileDeletion` record per storage key **inside the same transaction** as the
  row removal, then commit. The record is the only signal that a file must die.
- A `FileDeletionSweeper` `BackgroundService` (5-minute interval) processes pending
  deletions first, then reconciles disk: any stored file referenced by no candidate,
  no CV document, and no pending deletion — and older than a 1-hour safe-start
  threshold (so it cannot belong to an in-flight import) — is deleted.
- Pending-deletion rows are hard-deleted on success; the structured log is the audit
  trail. Sweep failures log-and-continue and are retried on the next pass.
- `PrivateFileStorage.DeleteAsync` now lets real failures throw (the `TryDelete`
  swallow is removed) and gains `ListStorageKeysAsync` / `IsOlderThanAsync` for the
  reconciler. `File.Delete`'s missing-file tolerance is retained (idempotent deletes).
- Import keeps its catch-block fast-path compensation for routine failures, but a
  compensation delete failure now logs and records a `PendingFileDeletion` instead of
  being swallowed; the reconciler covers the crash case.
- `PendingFileDeletion` lives in the Application layer as plumbing (it has no glossary
  term); the sweeper and storage implementation live in Infrastructure, registered in
  the composition root.

## Consequences

- Removal is **eventual**: the HTTP response means "the row is gone and the file is
  queued," not "the file is already gone." Glossary promises (Purge, Candidate
  Removal) are convergence guarantees, not in-request guarantees.
- Cleanup failure never fails the request, but is always detectable: an unprocessed
  `pending_file_deletion` row plus a structured error log.
- One seam serves all three sites (delete, purge, import compensation); no handler
  calls `DeleteAsync` directly.
- Existing databases need the `AddPendingFileDeletion` migration applied (automatic
  at startup via `MigrateAsync`).
