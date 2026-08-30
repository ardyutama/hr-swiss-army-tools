# Testing — Flow-First

This guide decides **what gets tested** in this repository. It is the single source of
truth for test scope; `docs/agents/vue.md` and `docs/agents/dotnet.md` keep only
stack-specific mechanics (Vitest/VTU setup, Testcontainers, xUnit). Recorded as
`docs/adr/0003-flow-first-testing.md`.

## Terms

**Flow Test**:
A test that proves a user story or business-process path end to end at a declared seam,
structured as the scenario HR would actually perform.

**State Test**:
A test that proves rendering or simple behavior of a unit in isolation (empty state, badge
text, hidden button, helper function output).

## The rule

Tests exist to make sure critical functionality behaves as it should. Write **Flow Tests
for the business process and user flow**. Do not write State Tests for component mechanics
or simple behavior.

**The traceability filter.** Every test must name its business source in the test or
`describe`/class name:

- `US-17: HR flags a candidate and jumps to the next` — traces to
  `docs/discovery/03-user-stories.md`; or
- `domain: closed vacancy is read-only` — traces to a term in `CONTEXT.md`.

A test that cannot name one of these two sources does not get written. If the behavior
matters and has no source yet, the story or glossary term comes first — see
`docs/agents/domain.md`.

A State Test survives the filter **only when the state is itself a business rule**. Example:
a Closed Vacancy being read-only is a glossary rule, so "hides row actions for a closed
vacancy" is a business assertion, not a UI assertion. Badge color, placeholder text, and
"the button renders" are not.

**Critical spine.** These stories carry the to-be business process and are mandatory: a
ticket touching any of them must land its Flow Test in the same ticket.

- US-9 — create vacancy (the process entry point)
- US-12 / US-13 — import candidates from `.eml`
- US-17 / US-18 — review: status, notes, match
- US-19 — contact: templates and prepared messages

Non-spine stories follow the traceability filter; reviewers judge per ticket.

## Seams (unchanged)

Seams are set by `docs/adr/0002-mvp-stack-and-api-seam.md` and do not move:

- **Backend**: xUnit + `WebApplicationFactory` + Testcontainers PostgreSQL. Every test
  crosses the HTTP seam against a hermetic database. Mock nothing inside the seam.
- **Frontend**: Vitest + Vue Test Utils + jsdom at the feature-component seam. Mock only
  `fetch` (and browser/platform modules the component genuinely owns, e.g. toasts). No
  Playwright or other E2E tooling without a separate ADR.

## Flow Test shape

Arrange the test as the scenario steps from the story. One `it` / `[Fact]` per scenario:
the happy path plus each business-rule violation. Assert only **user-observable outcomes**:
DOM content and emitted/toasted feedback on the client; status codes, response contracts,
and state re-fetched through the API on the backend.

Frontend example — one scenario, multiple steps, one seam mock:

```ts
it('US-9: HR creates a vacancy and sees it listed', async () => {
  const fetchMock = vi.fn((input: RequestInfo | URL, init?: RequestInit) => {
    if (init?.method === 'POST') return Promise.resolve(jsonResponse(vacancyDetails()))
    return Promise.resolve(jsonResponse([vacancy()]))
  })
  vi.stubGlobal('fetch', fetchMock)

  const wrapper = mount(VacancyListView)
  await flushPromises()
  // open dialog, fill title + requirement, submit
  // ...

  expect(toast.success).toHaveBeenCalledWith('Vacancy created successfully')
  expect(wrapper.text()).toContain('Senior Welder')
})
```

Backend example — a lifecycle scenario asserted through re-fetch:

```csharp
[Fact]
public async Task Close_vacancy_makes_it_read_only() // domain: closed vacancy is read-only
{
    using var client = factory.CreateClient();
    var location = await CreateVacancyAsync(client);
    await client.PostAsync($"{location}/close", content: null);

    var editResponse = await client.PutAsJsonAsync(location, new { title = "New title" });

    Assert.Equal(HttpStatusCode.BadRequest, editResponse.StatusCode);
}
```

## Pure-function unit tests

Allowed only when the function enforces a traceable business rule (e.g. vacancy form
validation mirroring domain invariants). A helper that exists for UI convenience (date
formatting, display mapping) gets no dedicated test — the Flow Test that displays its
output covers it.

## Existing suites

Tests written before this rule (the vacancy CRUD client specs and backend test classes) are
**frozen**: keep what passes, extend them only when a ticket changes their behavior, and
apply this rule to everything new. Reviewers do not flag pre-existing State Tests as
violations.
