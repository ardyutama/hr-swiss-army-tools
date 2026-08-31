# Hand-off plan: execution ticket breakdown

Type: grilling
Status: open
Blocked by: 03, 05

## Question

Define how the decided plan becomes execution tickets under `docs/agents/workflow.md`
once this map completes:

- Foundation as one ticket or three (split / kernel / migration).
- One execution ticket per slice (11) or grouped (vacancy spine batching).
- The done-gate per execution ticket (build + full suite green, `ca-review` clean).
- Where execution tickets live: continuation under `.scratch/ca-migration/issues/` or
  a fresh feature directory.
- Doc updates that land with execution: the `docs/agents/dotnet.md` "stand in"
  paragraph is removed once the four-project layout is real; ADR-0005 status note;
  repo memory updates (`backend-vsa.md`, `ef-migration-state.md`).
- The final acceptance check that declares the migration done: all 11 slices
  pack-shaped, `DomainValidationException`/`AddValidation()`/`EnsureCreated()` gone,
  `ca-review` clean, full suite green, seam byte-stable throughout.

## Comments
