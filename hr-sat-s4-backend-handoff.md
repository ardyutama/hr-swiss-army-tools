# Handoff — S4 review workspace: backend slices remaining

**Date:** 2026-09-02
**Repo:** `c:\Users\AU1833\Documents\personal\hr-swiss-army-tools`
**Stage reached:** The client-side review workspace (S4) is implemented, seam-tested
(23/23 client tests green), and passed a two-axis code review (Standards + Spec, 0
findings). **What remains for issue 06 is the backend tier** — the two PUT slices the
client already calls — plus the final verification pass.

## What the next session should do

Finish issue [.scratch/mvp/issues/06-review-workspace.md](.scratch/mvp/issues/06-review-workspace.md):

1. Backend slice 1 — **update notes**: `PUT /api/vacancies/{vid}/candidates/{cid}/notes`
2. Backend slice 2 — **set review status** (decision-as-commit, ADR-0008 #9):
   `PUT /api/vacancies/{vid}/candidates/{cid}/review`
3. Backend tests for both slices (HTTP seam + handler + validator, per pack rules).
4. Final pass: full backend suite + full client suite, then re-run the code-review skill
   over the new backend diff.
5. Tick the issue 06 checklist boxes that are now true.

Do NOT redo the client page. Do NOT re-run any design interview — everything is settled.
Do NOT commit — the user pushes themselves (AGENTS.md).

## Endpoint contract (client already implements this — match it exactly)

| Endpoint | Payload | Returns |
|---|---|---|
| `PUT …/candidates/{cid}/notes` | `{ notes }` — string, ≤ 4000 chars | `CandidateDetailsResponse` |
| `PUT …/candidates/{cid}/review` | `{ reviewStatus, notes? }` — reviewStatus is one of `flagged\|shortlisted\|rejected` | `CandidateDetailsResponse` |

- Decision-as-commit: when `notes` is present on the review call, persist it in the same
  transaction as the status change.
- Client sends `notes` as a plain string (possibly empty); empty/whitespace should store
  as `null` if that matches existing trimming conventions.
- Validation per ADR-0004 (FluentValidation on the command; notes >4000 → 400).
- 404 when the candidate does not belong to the vacancy (cross-vacancy guard — there is
  an existing test expectation for this).
- Closed vacancy stays read-only: mirror the existing `vacancy.EnsureCanModifyCandidate()`
  guard used by the Extract slice.
- Vacancy progress numerator needs NO backend change — it is derived from existing counts.

## Authoritative sources — read these, don't re-derive

- **Design/decisions:** `C:\Users\AU1833\AppData\Local\Temp\hr-sat-review-workspace-handoff.md`
  (the earlier S4 handoff: confirmed layout, decisions, and the test list) and
  [docs/adr/0008-client-design-language.md](docs/adr/0008-client-design-language.md) #9
  (notes commit contract).
- **Glossary:** [CONTEXT.md](CONTEXT.md) — Review Status `new|flagged|shortlisted|rejected`;
  Vacancy Progress; "decision" verbs.
- **Repo rules:** [AGENTS.md](AGENTS.md), `docs/agents/dotnet.md`,
  `docs/agents/workflow.md` (tests after implementation), `docs/agents/testing.md`
  (flow-first, traceability — these slices touch critical spine US-17/US-18).
- **What the client expects:** `src/hr-sat.Client/src/features/review/api.ts`
  (`updateCandidateNotes`, `updateCandidateReview`) and the seam tests in
  `src/hr-sat.Client/src/pages/review/ReviewView.spec.ts`.

## Backend implementation pointers (patterns verified this session)

Follow the existing candidate slices — the **Extract** and **SelectPrimaryCv** slices are
the closest templates:

- Command + handler + validator per slice under `src/hr-sat.Application/Features/Candidates/`:
  mirror `Extract/ExtractCandidateCommand.cs`, `ExtractCandidateCommandHandler.cs`,
  `ExtractCandidateCommandValidator.cs`. Validator shape:
  `RuleFor(command => command.VacancyId).GreaterThan(0);` etc. — add `notes` length ≤ 4000
  and a `reviewStatus` membership rule.
- Endpoint classes under `src/hr-sat.Web.Api/Endpoints/Candidates/` mirroring
  `Extract/Extract.cs`: `app.MapPut(...)`, `ICommandHandler<…, CandidateDetailsResponse>`,
  `result.Match<IResult>(TypedResults.Ok, CustomResults.Problem)`, `.WithTags(Tags.Candidates)`.
- Domain: `Candidate` (`src/hr-sat.Domain/Domain/Candidates/Candidate.cs`) has
  `ReviewStatus` / `Notes` with private setters — add mutation methods on the entity
  (e.g. `SetNotes` / `SetReviewStatus`), keeping the existing `ValidateOptionalText` /
  `TrimOptional` helpers' style. Enum is `CandidateReviewStatus` (`New|Flagged|Shortlisted|Rejected`).
- Response: `CandidateDetailsResponse.From(vacancyId, candidate, vacancy.Requirements.Select(r => r.Phrase))`.
- Handler test template: `tests/hr-sat.Tests/Candidates/ExtractCandidateHandlerTests.cs`
  with `CandidateTestData.SeedCandidateAsync` + `TestDbContext`; HTTP-seam template:
  `CandidateExtractionFlowTests.cs` / `ListCandidatesTests.cs` style (`ApiFactory` +
  Testcontainers, Docker must be running; if the dev server locks Debug DLLs run tests
  with `-c Release`).

Required tests (from the confirmed spec):
- notes persist;
- review sets status **and** commits notes in one call;
- invalid reviewStatus → 400;
- cross-vacancy candidate → 404;
- closed vacancy → read-only failure (mirror Extract's guard test if one exists).

## State of the working tree

- Uncommitted changes from this session (client S4) plus pre-session bookkeeping edits
  (issue 06 rewording, ADR-0008 #8/#2, "Match" header, issue 09 file) are all in the
  working tree — leave them; the user commits.
- Client commands: `npm run type-check`, `npm run lint`, `npm run test` in
  `src/hr-sat.Client`. Backend: `dotnet build hr-sat.slnx --nologo --no-restore -v:q`,
  full suite `dotnet test tests/hr-sat.Tests/hr-sat.Tests.csproj` (needs Docker).

## Suggested skills for the next agent

Call the Skill tool for, in order:

1. **vertical-slice-dotnet** (`add-feature`, `add-tests`, `ca-review`) — required pack
   for the two backend slices; named the source of truth per ADR-0005/AGENTS.md.
2. **dotnet-skills** conditional playbooks — only for the matching task at hand.
3. **code-review** — after implementation, over the backend diff (fixed point `HEAD`
   with `git add -N` for new files, as done this session).
4. **domain-modeling** — only if a new term surfaces (e.g. naming the review command);
   glossary work for this slice is done.

No sensitive information is contained in this handoff.
