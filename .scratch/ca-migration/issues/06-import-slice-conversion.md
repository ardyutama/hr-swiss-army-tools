# ImportCandidates conversion specifics

Type: grilling
Status: open
Blocked by: 05

## Question

The Import slice drags the most baggage; decide its pack-conformant shape:

- `EmlParser` placement: MimeKit parsing is technical — Infrastructure service, or
  feature-local under Candidates? Weigh against the sharing taxonomy.
- `ImportFilePreparer` and `IPrivateFileStorage` homes after the split.
- How the deliberate `Import/` folder name survives the pack's `{Feature}/{UseCase}/`
  layout: renaming to `ImportCandidates/` collides the namespace with the
  `ImportCandidates` class (repo memory, `backend-vsa.md` — do not "fix" blindly).
- `ImportContracts` DTOs mapped into command shape.
- `CandidateErrors` catalog entries for import failures (parse failure, duplicate
  source email, closed vacancy).
- Test coverage: which parts earn unit tests (parser? preparer?) vs integration-only
  at the Testcontainers seam (existing `ImportCandidatesTests` port).

## Comments
