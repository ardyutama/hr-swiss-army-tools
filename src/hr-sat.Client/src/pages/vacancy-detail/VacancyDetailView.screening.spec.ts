import { DOMWrapper, flushPromises, type VueWrapper } from '@vue/test-utils'
import { describe, expect, it, vi } from 'vitest'

import {
  bobSummary,
  candidateSummary,
  closedRound,
  dialogButton,
  formLayoutDto,
  jsonResponse,
  mountView,
  mountViewWithQuery,
  openRound,
  pagedCandidates,
  pagedCandidatesFor,
  stubFetch,
  toastAdd,
  vacancyDetails,
  type CandidateListItem,
  type FetchHandler,
} from './mountVacancyDetail'

vi.mock('@nuxt/ui/composables/useToast', () => ({
  useToast: () => ({ add: toastAdd }),
}))

// The header snapshot behind every editor test is formLayoutDto's: four
// columns, ordinals 0-3 ("Timestamp", "Nama Lengkap", "Email aktif",
// "Pengalaman kerja").
const SAVED_RULES = [
  { ordinal: 1, operator: 'equals', value: 'Tidak' },
  { ordinal: 3, operator: 'is-empty', value: null },
]

function screeningRuleSet(rules: unknown[] = SAVED_RULES) {
  return { id: 7, vacancyId: 1, rules }
}

/**
 * The screening endpoints for one spec: GET reads `rules`, PUT records its
 * body and echoes it back, and the preview POST records its body and answers
 * `preview` (default: 41 of 298). Anything else falls through to the caller's
 * handler or stubFetch's defaults.
 */
function stubScreening(
  options: {
    rules?: unknown[]
    preview?: () => unknown
  } = {},
) {
  const previewBodies: unknown[] = []
  const putBodies: unknown[] = []
  const handler: FetchHandler = (url, init) => {
    if (init?.method === 'POST' && url.endsWith('/screening-rules/preview')) {
      previewBodies.push(JSON.parse(String(init.body)))
      return Promise.resolve(
        jsonResponse(options.preview?.() ?? { total: 298, screenedOut: 41, perRule: [] }),
      )
    }
    if (init?.method === 'PUT' && url.endsWith('/screening-rules')) {
      const body = JSON.parse(String(init.body)) as { rules: unknown[] }
      putBodies.push(body)
      return Promise.resolve(jsonResponse({ id: 7, vacancyId: 1, rules: body.rules }))
    }
    if (url.endsWith('/screening-rules')) {
      return Promise.resolve(jsonResponse(screeningRuleSet(options.rules ?? SAVED_RULES)))
    }
    return undefined
  }
  return { handler, previewBodies, putBodies }
}

/** Routes the two endpoints every editor test needs alongside the screening stub. */
function withScreeningFixture(screening: { handler: FetchHandler }): FetchHandler {
  return (url, init) => {
    const handled = screening.handler(url, init)
    if (handled !== undefined) {
      return handled
    }
    if (url.includes('/rounds/1/candidates')) {
      return Promise.resolve(jsonResponse(pagedCandidates([])))
    }
    if (url.endsWith('/form-layout')) {
      return Promise.resolve(jsonResponse(formLayoutDto()))
    }
    return undefined
  }
}

async function openScreeningEditor(wrapper: VueWrapper) {
  const button = wrapper
    .find('section[aria-label="Screening"]')
    .findAll('button')
    .find((candidate) => candidate.text().includes('Screening rules'))
  expect(button, 'a "Screening rules" button').toBeDefined()
  await button!.trigger('click')
  await flushPromises()
}

/** The editor's footer count line (the dialog teleports to document.body). */
function statusLine(): string {
  const line = document.body.querySelector('p[role="status"]')
  expect(line, 'the editor count line').not.toBeNull()
  return line!.textContent ?? ''
}

function topDialog(): Element {
  const dialogs = document.body.querySelectorAll('[role="dialog"]')
  const top = dialogs[dialogs.length - 1]
  if (!top) {
    throw new Error('Expected an open dialog in document.body')
  }
  return top
}

function last<T>(items: T[]): T {
  const item = items[items.length - 1]
  if (item === undefined) {
    throw new Error('Expected at least one entry')
  }
  return item
}

function pageableItems(count: number): CandidateListItem[] {
  return Array.from({ length: count }, (_, index) => candidateSummary(index + 1))
}

// Chip text renders server-side verbatim (decision 9) — the fixtures carry the
// strings the server would have rendered.
const CAROL_CHIPS = [
  'Nama Lengkap · equals "Tidak"',
  'Pengalaman kerja · is empty',
  'Email aktif · contains "gmail"',
]

function screenedCarol() {
  return candidateSummary(3, {
    sourceSenderName: 'Carol Screened',
    screenedOut: true,
    firedRules: CAROL_CHIPS.map((display, index) => ({ index, display })),
  })
}

describe('VacancyDetailView · screening rules (issue 03)', () => {
  it('editor: opens from the Screening section, previews the saved rules immediately, and renders the live count', async () => {
    const screening = stubScreening({
      preview: () => ({
        total: 298,
        screenedOut: 41,
        perRule: [
          { index: 0, screenedOut: 10 },
          { index: 1, screenedOut: 31 },
        ],
      }),
    })
    stubFetch(() => vacancyDetails(), withScreeningFixture(screening))

    const { wrapper } = mountView()
    await flushPromises()

    // The section summarizes the saved rules for the viewed round.
    const section = wrapper.find('section[aria-label="Screening"]')
    expect(section.text()).toContain('2 rules · 0 screened out in Round 1')

    // No preview fires before the editor opens.
    expect(screening.previewBodies).toHaveLength(0)

    await openScreeningEditor(wrapper)

    // The editor opened with the two saved rules…
    expect(document.body.textContent).toContain('Rule 1')
    expect(document.body.textContent).toContain('Rule 2')

    // …and the live count fired immediately (no debounce on open) with the
    // saved set — the dialog's opening change announce is payload-identical
    // and stays free.
    expect(screening.previewBodies).toEqual([
      {
        rules: [
          { ordinal: 1, operator: 'equals', value: 'Tidak' },
          { ordinal: 3, operator: 'is-empty', value: null },
        ],
      },
    ])
    expect(statusLine()).toBe('Would screen out 41 of 298 form candidates in the active round.')

    // Neither rule hits the all/nothing net.
    expect(document.body.textContent).not.toContain('matches no one')
    expect(document.body.textContent).not.toContain('screens out everyone')

    // Below the cap: the counter and an enabled Add rule.
    expect(document.body.textContent).toContain('2 of 5 rules')
    const addRule = dialogButton('Add rule')
    expect(addRule, 'an Add rule button').toBeDefined()
    expect((addRule as HTMLButtonElement).disabled).toBe(false)
    wrapper.unmount()
  })

  it('editor: a rule matching no one or everyone gets an inline amber warning', async () => {
    const screening = stubScreening({
      preview: () => ({
        total: 298,
        screenedOut: 298,
        perRule: [
          { index: 0, screenedOut: 0 },
          { index: 1, screenedOut: 298 },
        ],
      }),
    })
    stubFetch(() => vacancyDetails(), withScreeningFixture(screening))

    const { wrapper } = mountView()
    await flushPromises()
    await openScreeningEditor(wrapper)

    expect(document.body.textContent).toContain('Rule 1 matches no one — it will never fire.')
    expect(document.body.textContent).toContain('Rule 2 screens out everyone — check the column.')
    expect(statusLine()).toBe('Would screen out 298 of 298 form candidates in the active round.')
    wrapper.unmount()
  })

  it('editor: Add rule disables at five rules and the counter explains the cap', async () => {
    const fiveRules = [0, 1, 2, 3, 0].map((ordinal, index) => ({
      ordinal,
      operator: 'equals',
      value: `rule-${index}`,
    }))
    const screening = stubScreening({
      rules: fiveRules,
      preview: () => ({
        total: 10,
        screenedOut: 2,
        perRule: fiveRules.map((_, index) => ({ index, screenedOut: 1 })),
      }),
    })
    stubFetch(() => vacancyDetails(), withScreeningFixture(screening))

    const { wrapper } = mountView()
    await flushPromises()
    await openScreeningEditor(wrapper)

    expect(document.body.textContent).toContain('5 rules at most')
    expect(document.body.textContent).not.toContain('5 of 5 rules')
    const addRule = dialogButton('Add rule')
    expect(addRule, 'an Add rule button').toBeDefined()
    expect((addRule as HTMLButtonElement).disabled).toBe(true)
    wrapper.unmount()
  })

  it('editor: the live preview debounces draft edits', async () => {
    let previewCalls = 0
    const screening = stubScreening({
      rules: [{ ordinal: 1, operator: 'equals', value: 'Tidak' }],
      preview: () => {
        previewCalls += 1
        return previewCalls === 1
          ? { total: 10, screenedOut: 2, perRule: [{ index: 0, screenedOut: 2 }] }
          : { total: 10, screenedOut: 5, perRule: [{ index: 0, screenedOut: 5 }] }
      },
    })
    stubFetch(() => vacancyDetails(), withScreeningFixture(screening))

    const { wrapper } = mountView()
    await flushPromises()
    await openScreeningEditor(wrapper)
    expect(screening.previewBodies).toHaveLength(1)
    expect(statusLine()).toBe('Would screen out 2 of 10 form candidates in the active round.')

    const valueInput = new DOMWrapper(
      document.body.querySelector('input[aria-label="Rule 1 value"]') as HTMLElement,
    )
    vi.useFakeTimers()
    try {
      await valueInput.setValue('Bacik')
      // Nothing fires during the quiet period…
      expect(screening.previewBodies).toHaveLength(1)
      await vi.advanceTimersByTimeAsync(350)
      await flushPromises()
      // …then one preview carries the edited draft and the count follows.
      expect(screening.previewBodies).toHaveLength(2)
      expect(screening.previewBodies[1]).toEqual({
        rules: [{ ordinal: 1, operator: 'equals', value: 'Bacik' }],
      })
      expect(statusLine()).toBe('Would screen out 5 of 10 form candidates in the active round.')
    } finally {
      vi.useRealTimers()
    }
    wrapper.unmount()
  })

  it('editor: save is modal-atomic — PUT, toast, then the list and the vacancy header reload', async () => {
    let vacancyLoads = 0
    let candidateLoads = 0
    const screening = stubScreening({
      rules: [{ ordinal: 1, operator: 'equals', value: 'Tidak' }],
    })
    stubFetch(
      () => {
        vacancyLoads += 1
        return vacancyDetails()
      },
      (url, init) => {
        const handled = screening.handler(url, init)
        if (handled !== undefined) {
          return handled
        }
        if (url.includes('/rounds/1/candidates')) {
          candidateLoads += 1
          return Promise.resolve(jsonResponse(pagedCandidates([])))
        }
        if (url.endsWith('/form-layout')) {
          return Promise.resolve(jsonResponse(formLayoutDto()))
        }
        return undefined
      },
    )

    const { wrapper } = mountView()
    await flushPromises()
    await openScreeningEditor(wrapper)

    // mountView doesn't await the router's initial navigation, so the mount
    // settles its own loads; the cascade assertions are relative to them.
    const vacancyLoadsBeforeSave = vacancyLoads
    const candidateLoadsBeforeSave = candidateLoads

    const saveButton = dialogButton('Save rules')
    expect(saveButton, 'a Save rules button').toBeDefined()
    saveButton!.click()
    await flushPromises()

    expect(screening.putBodies).toEqual([
      { rules: [{ ordinal: 1, operator: 'equals', value: 'Tidak' }] },
    ])
    expect(toastAdd).toHaveBeenCalledWith(
      expect.objectContaining({ title: 'Screening rules saved', color: 'success' }),
    )
    // The modal-atomic cascade reloads the list and the vacancy header together.
    expect(candidateLoads).toBe(candidateLoadsBeforeSave + 1)
    expect(vacancyLoads).toBe(vacancyLoadsBeforeSave + 1)
    // The editor closed.
    await vi.waitFor(() => {
      expect(document.body.querySelector('[role="dialog"]')).toBeNull()
    })
    wrapper.unmount()
  })

  it('domain: a closed vacancy opens the editor read-only — no Save, Add, or Remove', async () => {
    const screening = stubScreening()
    stubFetch(
      () =>
        vacancyDetails({
          status: 'closed',
          closedAt: '2026-09-15T00:00:00Z',
          rounds: [closedRound()],
        }),
      (url, init) => {
        const handled = screening.handler(url, init)
        if (handled !== undefined) {
          return handled
        }
        if (url.includes('/rounds/1/candidates')) {
          return Promise.resolve(jsonResponse(pagedCandidates([candidateSummary(1)])))
        }
        if (url.endsWith('/form-layout')) {
          return Promise.resolve(jsonResponse(formLayoutDto()))
        }
        return undefined
      },
    )

    const { wrapper } = mountView()
    await flushPromises()
    await openScreeningEditor(wrapper)

    const columnSelect = document.body.querySelector<HTMLButtonElement>(
      'button[aria-label="Rule 1 column"]',
    )
    const operatorSelect = document.body.querySelector<HTMLButtonElement>(
      'button[aria-label="Rule 1 operator"]',
    )
    const valueInput = document.body.querySelector<HTMLInputElement>(
      'input[aria-label="Rule 1 value"]',
    )
    expect(columnSelect?.disabled).toBe(true)
    expect(operatorSelect?.disabled).toBe(true)
    expect(valueInput?.disabled).toBe(true)
    expect(dialogButton('Save rules')).toBeUndefined()
    expect(dialogButton('Add rule')).toBeUndefined()
    expect(document.body.querySelector('button[aria-label="Remove rule 1"]')).toBeNull()
    wrapper.unmount()
  })

  it('domain: without an active round the count note replaces the preview and no preview fires', async () => {
    const screening = stubScreening()
    stubFetch(
      () => vacancyDetails({ rounds: [closedRound()] }),
      withScreeningFixture(screening),
    )

    const { wrapper } = mountView()
    await flushPromises()
    await openScreeningEditor(wrapper)

    expect(statusLine()).toBe("No active round — saved rules apply to the next round's imports.")
    expect(screening.previewBodies).toHaveLength(0)
    wrapper.unmount()
  })

  it('domain: before the first import the section explains itself and the button stays disabled', async () => {
    stubFetch(
      () => vacancyDetails(),
      (url) => {
        if (url.includes('/rounds/1/candidates')) {
          return Promise.resolve(jsonResponse(pagedCandidates([])))
        }
        return undefined
      },
    )

    const { wrapper } = mountView()
    await flushPromises()

    // No header snapshot (the form-layout 404 default) and no rules yet.
    const section = wrapper.find('section[aria-label="Screening"]')
    expect(section.text()).toContain('Import form responses to set up screening rules.')
    const button = section
      .findAll('button')
      .find((candidate) => candidate.text().includes('Screening rules'))
    expect(button, 'the Screening rules button stays rendered but disabled').toBeDefined()
    expect((button!.element as HTMLButtonElement).disabled).toBe(true)
    wrapper.unmount()
  })

  it('editor: an orphaned ordinal stays selectable, and zero form candidates suppress the amber net', async () => {
    // A drift confirmation shrank the snapshot after the rule was saved:
    // ordinal 9 no longer exists among the four snapshot columns.
    const screening = stubScreening({
      rules: [{ ordinal: 9, operator: 'equals', value: 'Tidak' }],
      preview: () => ({ total: 0, screenedOut: 0, perRule: [{ index: 0, screenedOut: 0 }] }),
    })
    stubFetch(() => vacancyDetails(), withScreeningFixture(screening))

    const { wrapper } = mountView()
    await flushPromises()
    await openScreeningEditor(wrapper)

    // The orphan renders as its own option: keepable, removable, never a
    // validation block.
    const columnSelect = document.body.querySelector<HTMLButtonElement>(
      'button[aria-label="Rule 1 column"]',
    )
    expect(columnSelect?.textContent).toContain('Column 9 — not in the current header snapshot')
    expect(columnSelect?.disabled).toBe(false)
    expect(document.body.querySelector('button[aria-label="Remove rule 1"]')).not.toBeNull()

    // The preview fired (there IS an active round), but M=0 swaps the count
    // line and suppresses every per-rule warning — 0 of 0 is not a signal.
    expect(screening.previewBodies).toHaveLength(1)
    expect(statusLine()).toBe('No form candidates in the active round to screen.')
    expect(document.body.textContent).not.toContain('matches no one')
    expect(document.body.textContent).not.toContain('screens out everyone')
    wrapper.unmount()
  })

  it('list: the toggle reveals screened rows with verbatim rule chips and no delete affordance', async () => {
    const requestUrls: string[] = []
    const items = [candidateSummary(1), screenedCarol()]
    stubFetch(
      () => vacancyDetails(),
      (url) => {
        if (url.includes('/rounds/1/candidates')) {
          requestUrls.push(url)
          return Promise.resolve(jsonResponse(pagedCandidatesFor(url, items)))
        }
        return undefined
      },
    )

    const { wrapper, router } = mountView()
    await flushPromises()

    // Default: screened-out candidates are excluded; the badge carries the count.
    expect(wrapper.text()).toContain('Alice Applicant')
    expect(wrapper.text()).not.toContain('Carol Screened')
    const toggle = () => wrapper.find('[role="checkbox"]')
    expect(toggle().exists()).toBe(true)
    expect((toggle().element as HTMLButtonElement).disabled).toBe(false)
    expect(toggle().element.closest('div.ml-auto')?.textContent).toContain('1')

    await toggle().trigger('click')
    await flushPromises()

    // screened=all went into the URL and onto the wire; the screened row is back.
    expect(router.currentRoute.value.query.screened).toBe('all')
    expect(new URL(last(requestUrls), 'http://localhost').searchParams.get('screened')).toBe('all')
    expect(wrapper.text()).toContain('Carol Screened')

    // Two chips verbatim, the third collapsed behind "+1 more rules".
    const carolRow = wrapper.findAll('.crow').find((row) => row.text().includes('Carol Screened'))
    expect(carolRow, "Carol's row").toBeDefined()
    const chips = carolRow!.findAll('.crow__rule-chip')
    expect(chips.map((chip) => chip.text())).toEqual(CAROL_CHIPS.slice(0, 2))
    expect(carolRow!.text()).toContain('+1 more rules')

    // Screening never offers deletion…
    expect(carolRow!.find('button[aria-label="Delete candidate"]').exists()).toBe(false)
    // …while an unscreened row keeps it.
    const aliceRow = wrapper.findAll('.crow').find((row) => row.text().includes('Alice Applicant'))
    expect(aliceRow!.find('button[aria-label="Delete candidate"]').exists()).toBe(true)
    wrapper.unmount()
  })

  it('list: the toggle renders disabled when the round has no screened-out candidates', async () => {
    stubFetch(
      () => vacancyDetails(),
      (url) => {
        if (url.includes('/rounds/1/candidates')) {
          return Promise.resolve(jsonResponse(pagedCandidates([candidateSummary(1)])))
        }
        return undefined
      },
    )

    const { wrapper } = mountView()
    await flushPromises()

    // Unavailable-but-explained (the tooltip needs a hover): never hidden.
    const toggle = wrapper.find('[role="checkbox"]')
    expect(toggle.exists()).toBe(true)
    expect((toggle.element as HTMLButtonElement).disabled).toBe(true)
    expect(toggle.element.closest('div.ml-auto')?.textContent).toContain('0')
    wrapper.unmount()
  })

  it('list: when every candidate is screened out the empty state points at the toggle', async () => {
    const items = [
      screenedCarol(),
      candidateSummary(4, {
        sourceSenderName: 'Dave Screened',
        screenedOut: true,
        firedRules: [{ index: 0, display: 'Nama Lengkap · equals "Tidak"' }],
      }),
    ]
    stubFetch(
      () => vacancyDetails(),
      (url) => {
        if (url.includes('/rounds/1/candidates')) {
          return Promise.resolve(jsonResponse(pagedCandidatesFor(url, items)))
        }
        return undefined
      },
    )

    const { wrapper, router } = mountView()
    await flushPromises()

    expect(wrapper.findAll('.crow')).toHaveLength(0)
    expect(wrapper.text()).toContain('All candidates in this round are screened out')

    const showScreened = wrapper
      .findAll('button')
      .find((button) => button.text().includes('Show screened out'))
    expect(showScreened, 'a Show screened out action').toBeDefined()
    await showScreened!.trigger('click')
    await flushPromises()

    expect(router.currentRoute.value.query.screened).toBe('all')
    expect(wrapper.text()).toContain('Carol Screened')
    expect(wrapper.text()).toContain('Dave Screened')
    wrapper.unmount()
  })

  it('list: the pager drives page into the URL and any filter change resets it', async () => {
    const requestUrls: string[] = []
    stubFetch(
      () => vacancyDetails(),
      (url) => {
        if (url.includes('/rounds/1/candidates')) {
          requestUrls.push(url)
          return Promise.resolve(jsonResponse(pagedCandidatesFor(url, pageableItems(101))))
        }
        return undefined
      },
    )

    const { wrapper, router } = mountView()
    await flushPromises()

    // The position counter and the pager (101 rows over pages of 100).
    const counter = wrapper.findAll('p').find((p) => p.text().includes('1-100 of 101'))
    expect(counter, 'a "1-100 of 101" position counter').toBeDefined()
    const pageTwo = Array.from(
      counter!.element.parentElement!.querySelectorAll('button'),
    ).find((button) => button.textContent?.trim() === '2')
    expect(pageTwo, 'a pager button for page 2').toBeDefined()

    await new DOMWrapper(pageTwo as HTMLElement).trigger('click')
    await flushPromises()

    expect(router.currentRoute.value.query.page).toBe('2')
    expect(new URL(last(requestUrls), 'http://localhost').searchParams.get('page')).toBe('2')
    expect(wrapper.text()).toContain('101-101 of 101')

    // A filter change resets to page 1 (and page 1 is omitted from the URL).
    const newChip = wrapper
      .findAll('[aria-label="Filter by review status"] button')
      .find((button) => button.text().includes('New'))
    await newChip!.trigger('click')
    await flushPromises()

    expect(router.currentRoute.value.query).toEqual({ status: 'new' })
    const lastUrl = new URL(last(requestUrls), 'http://localhost')
    expect(lastUrl.searchParams.get('status')).toBe('new')
    expect(lastUrl.searchParams.get('page')).toBeNull()
    wrapper.unmount()
  })

  it('list: a deep link restores status, outcome, query, sort, page, and the screened toggle', async () => {
    const requestUrls: string[] = []
    stubFetch(
      () => vacancyDetails(),
      (url) => {
        if (url.includes('/rounds/1/candidates')) {
          requestUrls.push(url)
          return Promise.resolve(
            jsonResponse(
              pagedCandidatesFor(url, [
                bobSummary({ reviewStatus: 'shortlisted', hireOutcome: 'hired' }),
              ]),
            ),
          )
        }
        return undefined
      },
    )

    const { wrapper } = await mountViewWithQuery('1', {
      status: 'shortlisted',
      outcome: 'hired',
      query: 'bob',
      sort: 'oldest',
      page: '2',
      screened: 'all',
    })
    await flushPromises()

    // The list request carried every filter verbatim.
    const params = new URL(requestUrls[0]!, 'http://localhost').searchParams
    expect(params.get('status')).toBe('shortlisted')
    expect(params.get('outcome')).toBe('hired')
    expect(params.get('query')).toBe('bob')
    expect(params.get('sort')).toBe('oldest')
    expect(params.get('page')).toBe('2')
    expect(params.get('screened')).toBe('all')

    // The search box restored its draft too.
    expect(
      (wrapper.find('input[aria-label="Search candidates"]').element as HTMLInputElement).value,
    ).toBe('bob')
    wrapper.unmount()
  })

  it('list: a stale page renders the recoverable empty state — never an auto-redirect', async () => {
    stubFetch(
      () => vacancyDetails(),
      (url) => {
        if (url.includes('/rounds/1/candidates')) {
          return Promise.resolve(jsonResponse(pagedCandidatesFor(url, pageableItems(50))))
        }
        return undefined
      },
    )

    // A deep-linked page 2 in a 50-row round comes back empty.
    const { wrapper, router } = await mountViewWithQuery('1', { page: '2' })
    await flushPromises()

    expect(wrapper.text()).toContain('This page is empty — candidates may have been reclassified.')
    // The URL is left alone until HR acts.
    expect(router.currentRoute.value.query.page).toBe('2')

    const back = wrapper
      .findAll('button')
      .find((button) => button.text().includes('Back to page 1'))
    expect(back, 'a Back to page 1 action').toBeDefined()
    await back!.trigger('click')
    await flushPromises()

    expect(router.currentRoute.value.query.page).toBeUndefined()
    expect(wrapper.findAll('.crow')).toHaveLength(50)
    expect(wrapper.text()).not.toContain('This page is empty')
    wrapper.unmount()
  })

  it('domain: a round switch keeps the filters and drops only the page', async () => {
    const roundOneUrls: string[] = []
    stubFetch(
      () =>
        vacancyDetails({
          rounds: [
            closedRound({ id: 1, roundNumber: 1, candidateCount: 1 }),
            openRound({ id: 2, roundNumber: 2, candidateCount: 3 }),
          ],
        }),
      (url) => {
        if (url.includes('/rounds/1/candidates')) {
          roundOneUrls.push(url)
          return Promise.resolve(
            jsonResponse(
              pagedCandidatesFor(url, [
                candidateSummary(9, { sourceSenderName: 'Round One Applicant' }),
              ]),
            ),
          )
        }
        if (url.includes('/rounds/2/candidates')) {
          return Promise.resolve(jsonResponse(pagedCandidatesFor(url, pageableItems(3))))
        }
        return undefined
      },
    )

    // Deep-linked onto the active round with a status filter and a page.
    const { wrapper, router } = await mountViewWithQuery('1', { status: 'new', page: '2' })
    await flushPromises()

    const roundOne = wrapper
      .find('[aria-label="Intake rounds"]')
      .findAll('button')
      .find((button) => button.text().includes('Round 1'))
    expect(roundOne, 'a Round 1 button').toBeDefined()
    await roundOne!.trigger('click')
    await flushPromises()

    // HR compares rounds through the same lens: the status filter survived…
    expect(router.currentRoute.value.query).toEqual({ status: 'new' })
    // …and only the page dropped, on the wire too.
    const lastUrl = new URL(last(roundOneUrls), 'http://localhost')
    expect(lastUrl.searchParams.get('status')).toBe('new')
    expect(lastUrl.searchParams.get('page')).toBeNull()
    expect(wrapper.text()).toContain('Round One Applicant')
    wrapper.unmount()
  })

  it('US-19: Send To All reads the unpaged messaging summary — a >100-candidate round comes back whole', async () => {
    stubFetch(
      () => vacancyDetails(),
      (url) => {
        // The paged list only ever serves page one…
        if (url.includes('/rounds/1/candidates')) {
          return Promise.resolve(jsonResponse(pagedCandidates(pageableItems(100))))
        }
        // …but the send flow reads the round's full unpaged summary.
        if (url.endsWith('/rounds/1/messaging-summary')) {
          return Promise.resolve(jsonResponse(pageableItems(101)))
        }
        if (url.endsWith('/vacancies/1/email-templates')) {
          return Promise.resolve(jsonResponse([]))
        }
        return undefined
      },
    )

    const { wrapper } = mountView()
    await flushPromises()

    const sendAll = wrapper
      .findAll('button')
      .find((button) => button.text().includes('Send email to all candidates'))
    expect(sendAll, 'a Send email to all candidates button').toBeDefined()
    await sendAll!.trigger('click')
    await flushPromises()

    // 101, not the paged list's 100.
    expect(topDialog().textContent).toContain('101 candidates in Round 1')
    wrapper.unmount()
  })

  it('US-17: review navigation mirrors the screened toggle and the page into the queue query', async () => {
    const items = [
      ...pageableItems(100),
      candidateSummary(101, { sourceSenderName: 'Page Two Applicant' }),
    ]
    stubFetch(
      () => vacancyDetails(),
      (url) => {
        if (url.includes('/rounds/1/candidates')) {
          return Promise.resolve(jsonResponse(pagedCandidatesFor(url, items)))
        }
        return undefined
      },
    )

    const { wrapper, router } = await mountViewWithQuery('1', { screened: 'all', page: '2' })
    await flushPromises()

    const row = wrapper.findAll('.crow').find((candidate) => candidate.text().includes('Page Two Applicant'))
    expect(row, 'the page-2 row').toBeDefined()
    await row!.trigger('click')
    await flushPromises()

    expect(router.currentRoute.value.name).toBe('candidate-review')
    expect(router.currentRoute.value.params).toMatchObject({
      id: '1',
      roundId: '1',
      candidateId: '101',
    })
    expect(router.currentRoute.value.query).toMatchObject({ page: '2', screened: 'all' })
    wrapper.unmount()
  })

  it('US-14: the search placeholder reads "Name, email, or phone" when the vacancy has a form layout', async () => {
    stubFetch(
      () => vacancyDetails(),
      (url) => {
        if (url.includes('/rounds/1/candidates')) {
          return Promise.resolve(jsonResponse(pagedCandidates([candidateSummary(1)])))
        }
        if (url.endsWith('/form-layout')) {
          return Promise.resolve(jsonResponse(formLayoutDto()))
        }
        return undefined
      },
    )

    const { wrapper } = mountView()
    await flushPromises()

    expect(wrapper.find('input[aria-label="Search candidates"]').attributes('placeholder')).toBe(
      'Name, email, or phone',
    )
    wrapper.unmount()
  })

  it('US-14: the search placeholder reads "Name, sender email, or subject" without a form layout', async () => {
    stubFetch(
      () => vacancyDetails(),
      (url) => {
        if (url.includes('/rounds/1/candidates')) {
          return Promise.resolve(jsonResponse(pagedCandidates([candidateSummary(1)])))
        }
        return undefined
      },
    )

    const { wrapper } = mountView()
    await flushPromises()

    expect(wrapper.find('input[aria-label="Search candidates"]').attributes('placeholder')).toBe(
      'Name, sender email, or subject',
    )
    wrapper.unmount()
  })
})
