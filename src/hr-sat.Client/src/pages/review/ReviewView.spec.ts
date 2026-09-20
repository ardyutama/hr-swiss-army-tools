import { DOMWrapper, flushPromises, mount, type VueWrapper } from '@vue/test-utils'
import { createMemoryHistory, createRouter, type Router } from 'vue-router'
import { afterEach, describe, expect, it, vi } from 'vitest'

import ReviewView from './ReviewView.vue'
import SpecAppShell from './SpecAppShell.vue'
import { formatReceivedAt } from '@/features/candidates/format'

const toastAdd = vi.fn()

vi.mock('@nuxt/ui/composables/useToast', () => ({
  useToast: () => ({ add: toastAdd }),
}))

// PDF.js cannot run under jsdom; stub the viewer's platform component so the
// seam tests can still exercise pagination state through its `loaded` event.
vi.mock('vue-pdf-embed', () => ({
  default: {
    name: 'VuePdfEmbed',
    props: ['source', 'page', 'width'],
    emits: ['loaded', 'loading-failed'],
    mounted(this: { $emit: (event: string, payload: unknown) => void }) {
      this.$emit('loaded', { numPages: 5 })
    },
    template: '<div data-testid="pdf-page" :data-page="page" />',
  },
}))

function jsonResponse(body: unknown, status = 200): Response {
  return new Response(JSON.stringify(body), {
    status,
    headers: { 'Content-Type': 'application/json' },
  })
}

function openRound(overrides: Partial<Record<string, unknown>> = {}) {
  return {
    id: 1,
    roundNumber: 1,
    name: null,
    status: 'open',
    closedAt: null,
    candidateCount: 2,
    ...overrides,
  }
}

function vacancyDetails(overrides: Partial<Record<string, unknown>> = {}) {
  return {
    id: 1,
    title: 'Accountant',
    openedOn: '2026-08-12',
    status: 'open',
    closedAt: null,
    createdAt: '2026-08-12T00:00:00Z',
    requirements: [
      { id: 11, phrase: 'Excel', position: 0 },
      { id: 12, phrase: 'VAT reporting', position: 1 },
      { id: 13, phrase: 'SAP', position: 2 },
    ],
    rounds: [openRound()],
    progress: { processedCandidates: 0, totalCandidates: 2 },
    ...overrides,
  }
}

const candidateNames = ['Jane Doe', 'Bob Builder', 'Ann Lee']

function candidateSummary(id: number, overrides: Record<string, unknown> = {}) {
  const name = candidateNames[id - 1] ?? `Candidate ${id}`
  return {
    id,
    fullName: name,
    contactEmail: `candidate${id}@mail.com`,
    contactPhone: null,
    notes: null,
    reviewStatus: 'new',
    hireOutcome: 'none',
    sourceSenderName: name,
    sourceSenderEmail: `candidate${id}@mail.com`,
    sourceSubject: `${name} application`,
    sourceSentAt: '2026-08-10T09:00:00Z',
    ...overrides,
  }
}

function candidateDetails(id: number, overrides: Record<string, unknown> = {}) {
  const name = candidateNames[id - 1] ?? `Candidate ${id}`
  return {
    id,
    reviewStatus: 'new',
    hireOutcome: 'none',
    promotedFromRoundNumber: null,
    promotedAt: null,
    priorApplications: [],
    fullName: name,
    contactEmail: `candidate${id}@mail.com`,
    notes: null,
    requirementReviews: [
      { requirementId: 11, confirmed: id === 1 },
      { requirementId: 12, confirmed: false },
      { requirementId: 13, confirmed: false },
    ],
    sourceSenderName: name,
    sourceSenderEmail: `candidate${id}@mail.com`,
    sourceSubject: 'Applying for the role',
    sourceBodyText: 'Please find my CV attached.',
    sourceSentAt: '2026-08-10T09:00:00Z',
    sourceOriginalFilename: `${id}.eml`,
    // Email-source defaults (issue 04); the form variant overrides them below.
    intakeSource: 'email',
    isResubmitted: false,
    formResponses: [] as Record<string, unknown>[],
    documents: [
      {
        id: id * 10,
        originalFilename: `cv-${id}.pdf`,
        sizeBytes: 2048,
        isPrimary: true,
        downloadUrl: `/api/vacancies/1/rounds/1/candidates/${id}/cv-documents/${id * 10}`,
      },
    ],
    ...overrides,
  }
}

// The Form Layout GET wire shape spells roles lowercased (see form-layout/api.ts);
// the review page fetches it lazily, only for form-sourced candidates. The
// columns are deliberately unordered: the panel must sort by ordinal.
function formLayoutDto(overrides: Partial<Record<string, unknown>> = {}) {
  return {
    id: 5,
    vacancyId: 1,
    headerSnapshot: ['Timestamp', 'Nama Lengkap', 'Email aktif', 'CV link', 'Pengalaman kerja', 'Posisi'],
    columns: [
      { ordinal: 1, role: 'name', label: null },
      { ordinal: 2, role: 'contactemail', label: null },
      { ordinal: 3, role: 'cvlink', label: null },
      { ordinal: 5, role: null, label: null },
      { ordinal: 4, role: null, label: 'Experience' },
    ],
    isValid: true,
    candidatesUpdated: 0,
    typedOverridesKept: 0,
    ...overrides,
  }
}

function formResponse(cells: string[], overrides: Record<string, unknown> = {}) {
  return {
    cells,
    formTimestampRaw: '2026-09-12T09:58:00Z',
    formTimestampParsed: '2026-09-12T09:58:00Z',
    isCurrent: true,
    importedAt: '2026-09-12T10:00:00Z',
    ...overrides,
  }
}

// A form-sourced candidate: no source email, no local PDFs; the stored raw
// Form Response row carries the details prefill, the CV link, and the answers.
function formCandidateDetails(id: number, overrides: Record<string, unknown> = {}) {
  return candidateDetails(id, {
    intakeSource: 'form',
    fullName: 'Siti Rahma',
    contactEmail: 'siti@example.com',
    sourceSenderName: null,
    sourceSenderEmail: null,
    sourceSubject: null,
    sourceBodyText: null,
    sourceSentAt: null,
    sourceOriginalFilename: null,
    documents: [],
    formResponses: [
      formResponse([
        '2026-09-12T09:58:00Z',
        'Siti Rahma',
        'siti@example.com',
        'https://drive.example.test/cv/siti',
        '3 years, PT X\nShift rotation',
        'Operator produksi',
      ]),
    ],
    ...overrides,
  })
}

interface CapturedRequest {
  url: string
  method: string
  body: Record<string, unknown> | undefined
}

function stubApi(
  options: {
    candidateCount?: number
    candidates?: ReturnType<typeof candidateSummary>[]
    details?: Record<number, Record<string, unknown>>
    vacancy?: Record<string, unknown>
    formLayout?: Record<string, unknown>
    formLayoutError?: { problem: unknown; status: number }
    mutationError?: { path: string; problem: unknown; status: number }
  } = {},
) {
  const count = options.candidateCount ?? 2
  const requests: CapturedRequest[] = []
  const mock = vi.fn((input: RequestInfo | URL, init?: RequestInit) => {
    const url = String(input)
    const method = init?.method ?? 'GET'
    const body = typeof init?.body === 'string' ? JSON.parse(init.body) : undefined
    requests.push({ url, method, body })

    if (options.mutationError && method === 'PUT' && url.endsWith(options.mutationError.path)) {
      return Promise.resolve(
        jsonResponse(options.mutationError.problem, options.mutationError.status),
      )
    }

    if (method === 'PUT' && url.endsWith('/details')) {
      const id = Number(/\/candidates\/(\d+)\/details/.exec(url)?.[1])
      return Promise.resolve(
        jsonResponse(
          candidateDetails(id, {
            fullName: body?.fullName,
            contactEmail: body?.contactEmail,
          }),
        ),
      )
    }
    if (method === 'PUT' && url.endsWith('/review')) {
      const id = Number(/\/candidates\/(\d+)\/review/.exec(url)?.[1])
      return Promise.resolve(
        jsonResponse(
          candidateDetails(id, {
            ...options.details?.[id],
            reviewStatus: body?.reviewStatus,
            notes: body?.notes,
          }),
        ),
      )
    }
    if (method === 'PUT' && url.endsWith('/outcome')) {
      const id = Number(/\/candidates\/(\d+)\/outcome/.exec(url)?.[1])
      const previousNotes = (options.details?.[id]?.notes as string | null | undefined) ?? null
      const outcomeNote = typeof body?.note === 'string' ? body.note : null
      return Promise.resolve(
        jsonResponse(
          candidateDetails(id, {
            ...options.details?.[id],
            hireOutcome: body?.outcome,
            notes: outcomeNote
              ? previousNotes
                ? `${previousNotes}\n${outcomeNote}`
                : outcomeNote
              : previousNotes,
          }),
        ),
      )
    }
    if (method === 'PUT' && url.endsWith('/notes')) {
      const id = Number(/\/candidates\/(\d+)\/notes/.exec(url)?.[1])
      return Promise.resolve(jsonResponse(candidateDetails(id, { notes: body?.notes })))
    }
    if (method === 'PUT' && url.includes('/requirement-reviews/')) {
      const id = Number(/\/candidates\/(\d+)\/requirement-reviews/.exec(url)?.[1])
      const requirementId = Number(/requirement-reviews\/(\d+)$/.exec(url)?.[1])
      const details = candidateDetails(id)
      return Promise.resolve(
        jsonResponse(
          candidateDetails(id, {
            requirementReviews: details.requirementReviews.map((review) =>
              review.requirementId === requirementId
                ? { ...review, confirmed: body?.confirmed }
                : review,
            ),
          }),
        ),
      )
    }
    const detailsMatch = /\/candidates\/(\d+)$/.exec(url)
    if (method === 'GET' && detailsMatch) {
      const id = Number(detailsMatch[1])
      return Promise.resolve(jsonResponse(options.details?.[id] ?? candidateDetails(id)))
    }
    // The review queue is server-filtered: it always carries a query string
    // when filters are active, so match the path, never the tail.
    if (method === 'GET' && url.includes('/review-queue')) {
      return Promise.resolve(
        jsonResponse(
          options.candidates ??
            Array.from({ length: count }, (_, index) => candidateSummary(index + 1)),
        ),
      )
    }
    if (method === 'GET' && url.endsWith('/vacancies/1')) {
      return Promise.resolve(jsonResponse(options.vacancy ?? vacancyDetails()))
    }
    // Option-gated on purpose: the review page must never fetch a layout for an
    // email candidate, so the unstubbed-fetch throw below stays the tripwire.
    if (method === 'GET' && url.endsWith('/form-layout')) {
      if (options.formLayoutError) {
        return Promise.resolve(
          jsonResponse(options.formLayoutError.problem, options.formLayoutError.status),
        )
      }
      if (options.formLayout) {
        return Promise.resolve(jsonResponse(options.formLayout))
      }
    }
    throw new Error(`Unstubbed fetch: ${method} ${url}`)
  })
  vi.stubGlobal('fetch', mock)
  return { requests }
}

async function mountReview(
  startCandidateId = '1',
  query: Record<string, string> = {},
): Promise<{
  wrapper: VueWrapper
  router: Router
}> {
  const router = createRouter({
    history: createMemoryHistory(),
    routes: [
      { path: '/vacancies/:id', name: 'vacancy-detail', component: { template: '<div />' } },
      {
        path: '/vacancies/:id/rounds/:roundId/review/:candidateId',
        name: 'candidate-review',
        component: ReviewView,
        props: true,
      },
    ],
  })
  await router.push({
    name: 'candidate-review',
    params: { id: '1', roundId: '1', candidateId: startCandidateId },
    query,
  })
  // The UApp shell provides the TooltipProvider the review header's
  // Resubmitted tooltip injects (a bare router-view mount crashes on it).
  const wrapper = mount(SpecAppShell, { attachTo: document.body, global: { plugins: [router] } })
  return { wrapper, router }
}

function findButton(wrapper: VueWrapper, label: string): DOMWrapper<HTMLButtonElement> {
  const button = wrapper
    .findAll('button')
    .find((candidate) => candidate.text().includes(label))
  expect(button, `a "${label}" button`).toBeDefined()
  return button as unknown as DOMWrapper<HTMLButtonElement>
}

function hasButton(wrapper: VueWrapper, label: string): boolean {
  return wrapper.findAll('button').some((button) => button.text().includes(label))
}

function findMenuItem(label: string): DOMWrapper<HTMLElement> {
  const item = Array.from(document.body.querySelectorAll<HTMLElement>('[role="menuitem"]')).find((candidate) =>
    candidate.textContent?.includes(label),
  )
  expect(item, `a "${label}" menu item`).toBeDefined()
  return new DOMWrapper(item as HTMLElement)
}

async function openOutcomeMenu(wrapper: VueWrapper) {
  const trigger = wrapper.find('button[aria-label="Set hire outcome"], button[aria-label^="Hire outcome:"]')
  expect(trigger.exists(), 'the hire outcome menu trigger').toBe(true)
  await trigger.trigger('click')
  await flushPromises()
}

afterEach(() => {
  vi.clearAllMocks()
  vi.restoreAllMocks()
  vi.unstubAllGlobals()
  document.body.innerHTML = ''
})

describe('ReviewView', () => {
  it('US-17: the workspace opens as a skeleton and settles into the candidate review', async () => {
    stubApi()
    const { wrapper } = await mountReview()

    // Fetch is still in flight: the shape-matched skeleton stands in.
    expect(wrapper.find('[aria-label="Loading review workspace"]').exists()).toBe(true)

    await flushPromises()

    expect(wrapper.find('[aria-label="Loading review workspace"]').exists()).toBe(false)
    expect(wrapper.text()).toContain('Accountant')
    expect(wrapper.text()).toContain('Jane Doe')
    expect(wrapper.text()).toContain('candidate1@mail.com')
    expect(wrapper.text()).toContain('1 / 2')
    expect(wrapper.text()).toContain('1 / 3 confirmed')
    expect(wrapper.text()).toContain('Excel')
    expect(wrapper.text()).toContain('SAP')
    // The stubbed PDF reported five pages; pagination reflects it.
    expect(wrapper.text()).toContain('1 / 5')
    expect(wrapper.find('button[aria-label="Previous page"] kbd').text()).toBe('Shift+←')
    expect(wrapper.find('button[aria-label="Next page"] kbd').text()).toBe('Shift+→')
    wrapper.unmount()
  })

  it('US-17: HR sees a single prior application with its review status', async () => {
    stubApi({
      details: {
        1: candidateDetails(1, {
          priorApplications: [
            { roundNumber: 1, roundName: null, reviewStatus: 'rejected' },
          ],
        }),
      },
    })
    const { wrapper } = await mountReview()
    await flushPromises()

    expect(wrapper.find('[data-testid="prior-application-notice"]').text()).toContain(
      'This sender also applied in Round 1 — rejected.',
    )
    expect(wrapper.find('[data-testid="prior-application-notice"][role="alert"]').exists()).toBe(false)
    wrapper.unmount()
  })

  it('US-17: HR sees all prior applications in descending round order', async () => {
    stubApi({
      details: {
        1: candidateDetails(1, {
          priorApplications: [
            { roundNumber: 3, roundName: null, reviewStatus: 'shortlisted' },
            { roundNumber: 1, roundName: null, reviewStatus: 'rejected' },
          ],
        }),
      },
    })
    const { wrapper } = await mountReview()
    await flushPromises()

    expect(wrapper.find('[data-testid="prior-application-notice"]').text()).toContain(
      'This sender also applied in Round 3 — shortlisted; Round 1 — rejected.',
    )
    wrapper.unmount()
  })

  it('US-17: HR sees a named prior round in parentheses', async () => {
    stubApi({
      details: {
        1: candidateDetails(1, {
          priorApplications: [
            { roundNumber: 3, roundName: 'July wave', reviewStatus: 'shortlisted' },
          ],
        }),
      },
    })
    const { wrapper } = await mountReview()
    await flushPromises()

    expect(wrapper.text()).toContain('This sender also applied in Round 3 (July wave) — shortlisted.')
    wrapper.unmount()
  })

  it('US-17: HR sees new prior applications as not yet reviewed', async () => {
    stubApi({
      details: {
        1: candidateDetails(1, {
          priorApplications: [
            { roundNumber: 2, roundName: null, reviewStatus: 'new' },
          ],
        }),
      },
    })
    const { wrapper } = await mountReview()
    await flushPromises()

    expect(wrapper.text()).toContain('This sender also applied in Round 2 — not yet reviewed.')
    wrapper.unmount()
  })

  it('US-17: HR sees no prior application notice when the history is empty', async () => {
    stubApi()
    const { wrapper } = await mountReview()
    await flushPromises()

    expect(wrapper.find('[data-testid="prior-application-notice"]').exists()).toBe(false)
    wrapper.unmount()
  })

  it('US-17: a details response without prior applications remains reviewable', async () => {
    const legacyDetails = Object.fromEntries(
      Object.entries(candidateDetails(1)).filter(([key]) => key !== 'priorApplications'),
    )
    stubApi({ details: { 1: legacyDetails } })
    const { wrapper } = await mountReview()
    await flushPromises()

    expect(wrapper.text()).toContain('Jane Doe')
    expect(wrapper.find('[data-testid="prior-application-notice"]').exists()).toBe(false)
    wrapper.unmount()
  })

  it('US-17: candidate navigation announces a prior application when one exists', async () => {
    stubApi({
      details: {
        2: candidateDetails(2, {
          priorApplications: [
            { roundNumber: 1, roundName: null, reviewStatus: 'rejected' },
          ],
        }),
      },
    })
    const { wrapper } = await mountReview()
    await flushPromises()

    await findButton(wrapper, 'Next').trigger('click')
    await flushPromises()

    expect(wrapper.find('p.sr-only[aria-live="polite"]').text()).toBe(
      'Candidate 2 of 2: Bob Builder. Prior application in Round 1 — rejected.',
    )
    wrapper.unmount()
  })

  it('domain: a screened-out candidate shows the screening notice with its fired rules', async () => {
    stubApi({
      details: {
        1: candidateDetails(1, {
          screening: {
            screenedOut: true,
            firedRules: [
              { index: 0, display: 'Pengalaman kerja contains "fresh graduate"' },
              { index: 1, display: 'Email aktif is empty' },
            ],
          },
        }),
      },
    })
    const { wrapper } = await mountReview()
    await flushPromises()

    const notice = wrapper.find('[data-testid="screened-out-notice"]')
    expect(notice.exists()).toBe(true)
    expect(notice.text()).toContain('Screened out by 2 rules')
    expect(notice.text()).toContain('Pengalaman kerja contains "fresh graduate"')
    expect(notice.text()).toContain('Email aktif is empty')
    expect(notice.text()).toContain('Screening never decides for you')
    wrapper.unmount()
  })

  it('domain: a single fired rule singularizes the screening notice title', async () => {
    stubApi({
      details: {
        1: candidateDetails(1, {
          screening: {
            screenedOut: true,
            firedRules: [{ index: 0, display: 'Email aktif is empty' }],
          },
        }),
      },
    })
    const { wrapper } = await mountReview()
    await flushPromises()

    expect(wrapper.find('[data-testid="screened-out-notice"]').text()).toContain(
      'Screened out by 1 rule',
    )
    wrapper.unmount()
  })

  it('domain: a candidate screening never evaluated renders no notice', async () => {
    stubApi()
    const { wrapper } = await mountReview()
    await flushPromises()

    expect(wrapper.find('[data-testid="screened-out-notice"]').exists()).toBe(false)
    wrapper.unmount()
  })

  it('Promote: the review details show promotion history only when provenance exists', async () => {
    stubApi({
      details: {
        1: candidateDetails(1, {
          promotedFromRoundNumber: 1,
          promotedAt: '2026-09-10T14:30:00Z',
        }),
      },
    })
    const promotedView = await mountReview()
    await flushPromises()

    expect(promotedView.wrapper.text()).toContain('Promoted from Round 1 on 2026-09-10.')
    promotedView.wrapper.unmount()

    stubApi()
    const originalView = await mountReview()
    await flushPromises()

    expect(originalView.wrapper.text()).not.toContain('Promoted from Round')
    originalView.wrapper.unmount()
  })

  it('US-15: Shift+Arrow keys focus and paginate the CV without changing candidates', async () => {
    stubApi()
    const { wrapper, router } = await mountReview()
    await flushPromises()

    const viewer = wrapper.find('[aria-label="CV document pages"]')
    expect(viewer.attributes('tabindex')).toBe('0')
    expect(viewer.attributes('aria-keyshortcuts')).toBe('Shift+ArrowLeft Shift+ArrowRight')

    window.dispatchEvent(new KeyboardEvent('keydown', { key: 'ArrowRight', shiftKey: true }))
    await flushPromises()
    expect(wrapper.find('[data-testid="pdf-page"]').attributes('data-page')).toBe('2')
    expect(document.activeElement).toBe(viewer.element)
    expect(router.currentRoute.value.params.candidateId).toBe('1')

    const notes = wrapper.find('textarea[aria-label="Candidate notes"]')
    ;(notes.element as HTMLTextAreaElement).focus()
    window.dispatchEvent(new KeyboardEvent('keydown', { key: 'ArrowLeft', shiftKey: true }))
    await flushPromises()
    expect(wrapper.find('[data-testid="pdf-page"]').attributes('data-page')).toBe('2')
    expect(document.activeElement).toBe(notes.element)

    await viewer.trigger('focus')
    await viewer.trigger('keydown', { key: 'ArrowLeft', shiftKey: true })
    expect(wrapper.find('[data-testid="pdf-page"]').attributes('data-page')).toBe('1')

    await viewer.trigger('keydown', { key: 'ArrowLeft', shiftKey: true })
    expect(wrapper.find('[data-testid="pdf-page"]').attributes('data-page')).toBe('1')

    for (let pageNumber = 2; pageNumber <= 5; pageNumber += 1) {
      await viewer.trigger('keydown', { key: 'ArrowRight', shiftKey: true })
    }
    expect(wrapper.find('[data-testid="pdf-page"]').attributes('data-page')).toBe('5')

    await viewer.trigger('keydown', { key: 'ArrowRight', shiftKey: true })
    expect(wrapper.find('[data-testid="pdf-page"]').attributes('data-page')).toBe('5')
    wrapper.unmount()
  })

  it('US-17: HR can switch between multiple CV documents', async () => {
    stubApi({
      details: {
        1: candidateDetails(1, {
          documents: [
            {
              id: 10,
              originalFilename: 'cv-1.pdf',
              sizeBytes: 2048,
              isPrimary: true,
              downloadUrl: '/cv-documents/10',
            },
            {
              id: 11,
              originalFilename: 'references.pdf',
              sizeBytes: 1024,
              isPrimary: false,
              downloadUrl: '/cv-documents/11',
            },
          ],
        }),
      },
    })
    const { wrapper } = await mountReview()
    await flushPromises()

    const viewer = wrapper.find('[aria-label="CV document pages"]')
    await viewer.trigger('keydown', { key: 'ArrowRight', shiftKey: true })
    expect(wrapper.find('[data-testid="pdf-page"]').attributes('data-page')).toBe('2')

    const picker = wrapper.find('button[aria-label="Choose CV document"]')
    expect(picker.text()).toContain('cv-1.pdf')
    await picker.trigger('keydown', { key: 'ArrowDown' })
    await flushPromises()

    const option = Array.from(document.body.querySelectorAll<HTMLElement>('[role="option"]')).find(
      (candidate) => candidate.textContent?.includes('references.pdf'),
    )
    expect(option).toBeDefined()
    await new DOMWrapper(option as HTMLElement).trigger('keydown', { key: 'Enter' })
    await flushPromises()

    expect(picker.text()).toContain('references.pdf')
    expect(wrapper.find('[data-testid="pdf-page"]').attributes('data-page')).toBe('1')
    wrapper.unmount()
  })

  it('US-17: HR reviews the sorted, filtered candidate queue', async () => {
    // The server filters and sorts: the stub returns the already-narrowed
    // queue (oldest first: Bob 08-20, then Jane 08-30) and the test asserts
    // the request carried the list's URL state.
    const { requests } = stubApi({
      candidates: [
        candidateSummary(2, {
          reviewStatus: 'shortlisted',
          sourceSentAt: '2026-08-20T09:00:00Z',
        }),
        candidateSummary(1, {
          reviewStatus: 'shortlisted',
          sourceSentAt: '2026-08-30T09:00:00Z',
        }),
      ],
    })
    const { wrapper, router } = await mountReview('2', {
      status: 'shortlisted',
      sort: 'oldest',
    })
    await flushPromises()

    const queueRequest = requests.find((request) => request.url.includes('/review-queue'))
    expect(queueRequest?.url).toContain('status=shortlisted')
    expect(queueRequest?.url).toContain('sort=oldest')

    expect(wrapper.text()).toContain('Bob Builder')
    expect(wrapper.text()).toContain('1 / 2')

    await findButton(wrapper, 'Next').trigger('click')
    await flushPromises()

    expect(router.currentRoute.value.params.candidateId).toBe('1')
    expect(wrapper.text()).toContain('Jane Doe')
    wrapper.unmount()
  })

  it('US-17: HR shortlists with pending notes and lands on the next candidate', async () => {
    const { requests } = stubApi()
    const { wrapper, router } = await mountReview()
    await flushPromises()

    const notes = wrapper.find('textarea[aria-label="Candidate notes"]')
    await notes.setValue('Strong on VAT, no SAP')
    await findButton(wrapper, 'Shortlist').trigger('click')
    await flushPromises()

    expect(toastAdd).toHaveBeenCalledWith(expect.objectContaining({
      title: 'Candidate shortlisted successfully',
      color: 'success',
    }))

    // Decision-as-commit: one request carries the status and the pending notes.
    const reviewRequest = requests.find(
      (request) => request.method === 'PUT' && request.url.endsWith('/candidates/1/review'),
    )
    expect(reviewRequest?.body).toEqual({
      reviewStatus: 'shortlisted',
      notes: 'Strong on VAT, no SAP',
    })
    expect(router.currentRoute.value.params.candidateId).toBe('2')
    expect(wrapper.text()).toContain('Bob Builder')
    expect(wrapper.text()).toContain('2 / 2')
    expect(wrapper.text()).toContain('1 / 2 reviewed')
    wrapper.unmount()
  })

  it('US-17: HR must verify saved details before moving on from a shortlisted candidate', async () => {
    const { requests } = stubApi({
      details: { 1: candidateDetails(1, { fullName: null, contactEmail: null }) },
    })
    const { wrapper, router } = await mountReview()
    await flushPromises()

    await findButton(wrapper, 'Shortlist').trigger('click')
    await flushPromises()

    const reviewRequests = requests.filter(
      (request) => request.method === 'PUT' && request.url.endsWith('/candidates/1/review'),
    )
    expect(reviewRequests).toHaveLength(0)
    expect(router.currentRoute.value.params.candidateId).toBe('1')
    expect(toastAdd).toHaveBeenCalledWith({
      title:
        'Candidate details are not saved. Verify the name and email before moving to the next candidate.',
      color: 'warning',
      class: 'review-details-warning-toast',
    })
    expect(wrapper.text()).not.toContain('Candidate details are not saved.')

    await findButton(wrapper, 'Next').trigger('click')
    await flushPromises()

    expect(router.currentRoute.value.params.candidateId).toBe('1')

    await findButton(wrapper, 'Edit').trigger('click')
    await wrapper.find('input[aria-label="Candidate name"]').setValue('Jane Doe')
    await wrapper.find('input[aria-label="Candidate email"]').setValue('jane.doe@mail.com')
    await wrapper.find('form').trigger('submit')
    await flushPromises()

    await findButton(wrapper, 'Next').trigger('click')
    await flushPromises()

    expect(router.currentRoute.value.params.candidateId).toBe('2')
    wrapper.unmount()
  })

  it('US-17: keyboard shortcuts decide, and editable fields keep their keystrokes', async () => {
    const { requests } = stubApi()
    const { wrapper, router } = await mountReview()
    await flushPromises()

    // Typing "s" inside the notes editor must not trigger a decision.
    const notes = wrapper.find('textarea[aria-label="Candidate notes"]')
    notes.element.dispatchEvent(new KeyboardEvent('keydown', { key: 's', bubbles: true }))
    await flushPromises()
    expect(requests.some((request) => request.url.endsWith('/review'))).toBe(false)

    window.dispatchEvent(new KeyboardEvent('keydown', { key: 's' }))
    await flushPromises()

    const reviewRequest = requests.find((request) => request.url.endsWith('/candidates/1/review'))
    expect(reviewRequest?.body).toMatchObject({ reviewStatus: 'shortlisted' })
    expect(router.currentRoute.value.params.candidateId).toBe('2')
    expect(wrapper.find('p.sr-only[aria-live="polite"]').text()).toBe(
      'Shortlisted. Candidate 2 of 2: Bob Builder',
    )
    wrapper.unmount()
  })

  it('US-17: HR can edit candidate details and open the source email with keyboard shortcuts', async () => {
    stubApi()
    const { wrapper } = await mountReview()
    await flushPromises()

    const editButton = findButton(wrapper, 'Edit')
    const openEmailButton = findButton(wrapper, 'Open')
    expect(editButton.attributes('aria-keyshortcuts')).toBe('E')
    expect(editButton.find('kbd').text()).toBe('E')
    expect(openEmailButton.attributes('aria-keyshortcuts')).toBe('O')
    expect(openEmailButton.find('kbd').text()).toBe('O')

    window.dispatchEvent(new KeyboardEvent('keydown', { key: 'e' }))
    await flushPromises()
    expect(wrapper.find('form').exists()).toBe(true)

    const candidateName = wrapper.find('input[aria-label="Candidate name"]')
    ;(candidateName.element as HTMLInputElement).focus()
    expect(document.activeElement).toBe(candidateName.element)
    window.dispatchEvent(new KeyboardEvent('keydown', { key: 'o' }))
    await flushPromises()
    expect(document.body.querySelector('[role="dialog"]')).toBeNull()

    ;(candidateName.element as HTMLInputElement).blur()
    await findButton(wrapper, 'Cancel').trigger('click')
    window.dispatchEvent(new KeyboardEvent('keydown', { key: 'o' }))
    await flushPromises()
    expect(document.body.querySelector('[role="dialog"]')?.textContent).toContain('Source email')

    window.dispatchEvent(new KeyboardEvent('keydown', { key: 'o' }))
    await flushPromises()
    expect(document.body.querySelector('[role="dialog"]')).toBeNull()

    window.dispatchEvent(new KeyboardEvent('keydown', { key: 'o' }))
    await flushPromises()
    expect(document.body.querySelector('[role="dialog"]')?.textContent).toContain('Source email')

    window.dispatchEvent(new KeyboardEvent('keydown', { key: 'Escape' }))
    await flushPromises()
    expect(document.body.querySelector('[role="dialog"]')).toBeNull()
    wrapper.unmount()
  })

  it('US-17: E returns focus to an active details draft without resetting it', async () => {
    const { requests } = stubApi()
    const { wrapper } = await mountReview()
    await flushPromises()

    window.dispatchEvent(new KeyboardEvent('keydown', { key: 'e' }))
    await flushPromises()

    const nameInput = wrapper.find('input[aria-label="Candidate name"]')
    const emailInput = wrapper.find('input[aria-label="Candidate email"]')
    await nameInput.setValue('Updated Jane')
    await emailInput.setValue('updated.jane@mail.com')

    const viewer = wrapper.find('[aria-label="CV document pages"]')
    ;(viewer.element as HTMLElement).focus()
    expect(document.activeElement).toBe(viewer.element)

    window.dispatchEvent(new KeyboardEvent('keydown', { key: 'e' }))
    await flushPromises()

    expect(document.activeElement).toBe(nameInput.element)
    expect((nameInput.element as HTMLInputElement).value).toBe('Updated Jane')
    expect((emailInput.element as HTMLInputElement).value).toBe('updated.jane@mail.com')

    await wrapper.find('form').trigger('submit')
    await flushPromises()

    const detailsRequest = requests.find(
      (request) => request.method === 'PUT' && request.url.endsWith('/details'),
    )
    expect(detailsRequest?.body).toEqual({
      fullName: 'Updated Jane',
      contactEmail: 'updated.jane@mail.com',
    })
    wrapper.unmount()
  })

  it('US-17: N focuses Notes without capturing typed N characters', async () => {
    const { requests } = stubApi()
    const { wrapper } = await mountReview()
    await flushPromises()

    const notes = wrapper.find('textarea[aria-label="Candidate notes"]')
    const notesSection = wrapper.find('section[aria-label="Notes"]')
    expect(notesSection.find('kbd').text()).toBe('N')
    expect(
      notesSection.find('[title="Press N to focus Notes. Press Escape to save and leave."]').exists(),
    ).toBe(true)

    window.dispatchEvent(new KeyboardEvent('keydown', { key: 'n' }))
    await flushPromises()

    expect(document.activeElement).toBe(notes.element)

    await notes.setValue('candidate')
    window.dispatchEvent(new KeyboardEvent('keydown', { key: 'n' }))
    await flushPromises()

    expect(document.activeElement).toBe(notes.element)
    expect(
      requests.some((request) => request.method === 'PUT' && request.url.endsWith('/candidates/1/notes')),
    ).toBe(false)

    window.dispatchEvent(new KeyboardEvent('keydown', { key: 'Escape' }))
    await flushPromises()

    expect(document.activeElement).not.toBe(notes.element)
    const notesRequest = requests.find(
      (request) => request.method === 'PUT' && request.url.endsWith('/candidates/1/notes'),
    )
    expect(notesRequest?.body).toEqual({ notes: 'candidate' })
    wrapper.unmount()
  })

  it('US-17: Esc exits Editing mode and re-arms decision shortcuts', async () => {
    const { requests } = stubApi()
    const { wrapper, router } = await mountReview()
    await flushPromises()

    const notes = wrapper.find('textarea[aria-label="Candidate notes"]')
    await notes.trigger('focus')
    await notes.setValue('Ready for a decision')
    window.dispatchEvent(new KeyboardEvent('keydown', { key: 'Escape' }))
    await flushPromises()

    expect(document.activeElement).not.toBe(notes.element)

    window.dispatchEvent(new KeyboardEvent('keydown', { key: 's' }))
    await flushPromises()

    const reviewRequest = requests.find((request) => request.url.endsWith('/candidates/1/review'))
    expect(reviewRequest?.body).toMatchObject({ reviewStatus: 'shortlisted' })
    expect(router.currentRoute.value.params.candidateId).toBe('2')
    wrapper.unmount()
  })

  it('US-17: ? opens the keyboard shortcuts help modal', async () => {
    const { requests } = stubApi()
    const { wrapper } = await mountReview()
    await flushPromises()

    window.dispatchEvent(new KeyboardEvent('keydown', { key: '?' }))
    await flushPromises()

    const dialog = document.body.querySelector('[role="dialog"]')
    expect(dialog?.textContent).toContain('Keyboard shortcuts')
    expect(dialog?.textContent).toContain('Triage mode')
    expect(dialog?.textContent).toContain('Editing mode')
    expect(dialog?.textContent).toContain('Previous/next CV page')
    expect(dialog?.textContent).toContain('Shift+← / Shift+→')
    expect(dialog?.textContent).toContain('Edit candidate details')
    expect(dialog?.textContent).toContain('Open/close source email')
    expect(dialog?.textContent).toContain('Toggle requirement by position')
    expect(dialog?.textContent).toContain('Open CV link in a new tab')
    expect(dialog?.textContent).toContain('Triage mode, form candidates')

    window.dispatchEvent(new KeyboardEvent('keydown', { key: 's' }))
    await flushPromises()
    expect(requests.some((request) => request.url.endsWith('/review'))).toBe(false)

    window.dispatchEvent(new KeyboardEvent('keydown', { key: 'Escape' }))
    await flushPromises()
    expect(document.body.querySelector('[role="dialog"]')).toBeNull()
    wrapper.unmount()
  })

  it('US-17: HR can close the keyboard shortcuts help modal', async () => {
    stubApi()
    const { wrapper } = await mountReview()
    await flushPromises()

    window.dispatchEvent(new KeyboardEvent('keydown', { key: '?' }))
    await flushPromises()

    const closeButton = Array.from(document.body.querySelectorAll('button')).find((button) =>
      button.textContent?.trim() === 'Close',
    )
    expect(closeButton).toBeDefined()
    await new DOMWrapper(closeButton as HTMLButtonElement).trigger('click')
    await flushPromises()

    expect(document.body.querySelector('[role="dialog"]')).toBeNull()
    wrapper.unmount()
  })

  it('US-17: review action buttons expose their keyboard shortcuts', async () => {
    stubApi()
    const { wrapper } = await mountReview()
    await flushPromises()

    expect(findButton(wrapper, 'Prev').attributes('aria-keyshortcuts')).toBe('ArrowLeft')
    expect(findButton(wrapper, 'Next').attributes('aria-keyshortcuts')).toBe('ArrowRight')
    expect(findButton(wrapper, 'Shortlist').attributes('aria-keyshortcuts')).toBe('S')
    expect(findButton(wrapper, 'Flag').attributes('aria-keyshortcuts')).toBe('F')
    expect(findButton(wrapper, 'Reject').attributes('aria-keyshortcuts')).toBe('R')
    expect(wrapper.find('[aria-label="Hire outcomes"]').exists()).toBe(false)

    const shortcutButton = wrapper.find('button[aria-label="Keyboard shortcuts"]')
    expect(shortcutButton.exists()).toBe(true)
    expect(shortcutButton.attributes('aria-keyshortcuts')).toBe('?')
    expect(shortcutButton.text()).toContain('Shortcuts')
    expect(shortcutButton.text()).toContain('?')
    expect(findButton(wrapper, 'Next').classes().some((className) => className.includes('bg-primary'))).toBe(true)

    await shortcutButton.trigger('click')
    await flushPromises()
    expect(document.body.querySelector('[role="dialog"]')?.textContent).toContain(
      'Keyboard shortcuts',
    )
    wrapper.unmount()
  })

  it('domain: shortlisted candidates can record outcomes while the vacancy is open', async () => {
    const { requests } = stubApi({
      vacancy: vacancyDetails({ hiring: { neededHires: 2, activeHires: 0 } }),
      details: { 1: candidateDetails(1, { reviewStatus: 'shortlisted' }) },
    })
    const { wrapper } = await mountReview()
    await flushPromises()

    const outcomes = wrapper.find('[aria-label="Hire outcomes"]')
    expect(outcomes.exists()).toBe(true)
    expect(wrapper.find('button[aria-label="Set hire outcome"]').text()).toContain('Set outcome')

    await openOutcomeMenu(wrapper)
    expect(findMenuItem('Mark Hired').text()).toContain('⇧H')
    expect(findMenuItem('Mark Hired').attributes('aria-disabled')).toBeUndefined()
    expect(findMenuItem('Mark Runaway').attributes('aria-disabled')).toBe('true')
    expect(findMenuItem('Mark Declined').attributes('aria-disabled')).toBeUndefined()

    await findMenuItem('Mark Hired').trigger('click')
    await flushPromises()

    expect(requests.find((request) => request.url.endsWith('/outcome'))?.body).toEqual({
      outcome: 'hired',
    })
    expect(wrapper.text()).toContain('Hired')

    await openOutcomeMenu(wrapper)
    expect(findMenuItem('Mark Runaway').attributes('aria-disabled')).toBeUndefined()
    expect(findMenuItem('Mark Declined').attributes('aria-disabled')).toBe('true')
    await findMenuItem('Mark Runaway').trigger('click')
    await flushPromises()
    expect(document.body.querySelector('[role="dialog"]')?.textContent).toContain('Reopens 1 needed-hire slot')
    await new DOMWrapper(document.body.querySelector('[role="dialog"]') as HTMLElement).find('button').trigger('click')
    await flushPromises()

    await openOutcomeMenu(wrapper)
    await findMenuItem('Clear outcome').trigger('click')
    await flushPromises()
    expect(requests.filter((request) => request.url.endsWith('/outcome')).at(-1)?.body).toEqual({ outcome: 'none' })
    wrapper.unmount()
  })

  it('domain: hire outcome actions stay enabled in a closed round but not a closed vacancy', async () => {
    stubApi({
      vacancy: vacancyDetails({
        rounds: [openRound({ status: 'closed', closedAt: '2026-09-05T00:00:00Z' })],
      }),
      details: { 1: candidateDetails(1, { reviewStatus: 'shortlisted' }) },
    })
    const closedRoundView = await mountReview()
    await flushPromises()
    expect(hasButton(closedRoundView.wrapper, 'Shortlist')).toBe(false)
    expect(closedRoundView.wrapper.find('[aria-label="Review status: Shortlisted"]').exists()).toBe(true)
    await openOutcomeMenu(closedRoundView.wrapper)
    expect(findMenuItem('Mark Hired').attributes('aria-disabled')).toBeUndefined()
    closedRoundView.wrapper.unmount()

    stubApi({
      vacancy: vacancyDetails({ status: 'closed', closedAt: '2026-09-06T00:00:00Z' }),
      details: { 1: candidateDetails(1, { reviewStatus: 'shortlisted', hireOutcome: 'hired' }) },
    })
    const closedVacancyView = await mountReview()
    await flushPromises()
    expect(hasButton(closedVacancyView.wrapper, 'Shortlist')).toBe(false)
    expect(closedVacancyView.wrapper.find('[aria-label="Review status: Shortlisted"]').exists()).toBe(true)
    expect(closedVacancyView.wrapper.find('[aria-label="Hire outcome: Hired"]').exists()).toBe(true)
    expect(closedVacancyView.wrapper.find('[aria-label="Hire outcomes"]').exists()).toBe(false)
    closedVacancyView.wrapper.unmount()
  })

  it('domain: plain outcome keys are inert while Shift+H stays direct and Shift+U confirms', async () => {
    const { requests } = stubApi({
      details: { 1: candidateDetails(1, { reviewStatus: 'shortlisted' }) },
    })
    const { wrapper, router } = await mountReview()
    await flushPromises()

    window.dispatchEvent(new KeyboardEvent('keydown', { key: 'h' }))
    await flushPromises()
    expect(requests.filter((request) => request.url.endsWith('/outcome'))).toHaveLength(0)

    window.dispatchEvent(new KeyboardEvent('keydown', { key: 'h', shiftKey: true }))
    await flushPromises()
    expect(requests.filter((request) => request.url.endsWith('/outcome'))).toHaveLength(1)
    expect(requests.find((request) => request.url.endsWith('/outcome'))?.body).toEqual({ outcome: 'hired' })

    window.dispatchEvent(new KeyboardEvent('keydown', { key: 'u' }))
    await flushPromises()
    expect(requests.filter((request) => request.url.endsWith('/outcome'))).toHaveLength(1)
    expect(document.body.querySelector('[role="dialog"]')).toBeNull()

    window.dispatchEvent(new KeyboardEvent('keydown', { key: 'u', shiftKey: true }))
    await flushPromises()
    expect(document.body.querySelector('[role="dialog"]')?.textContent).toContain(
      'Reopens 1 needed-hire slot',
    )
    const outcomeNote = document.body.querySelector('textarea[aria-label="Outcome note"]')
    expect(document.activeElement).toBe(outcomeNote)
    await new DOMWrapper(outcomeNote as HTMLTextAreaElement).setValue('Left after starting.')
    await new DOMWrapper(outcomeNote as HTMLTextAreaElement).trigger('keydown', { key: 'Enter' })
    await flushPromises()
    const outcomeRequests = requests.filter((request) => request.url.endsWith('/outcome'))
    expect(outcomeRequests).toHaveLength(2)
    expect(outcomeRequests.at(-1)?.body).toEqual({ outcome: 'runaway', note: 'Left after starting.' })
    expect(router.currentRoute.value.params.candidateId).toBe('2')
    await flushPromises()
    wrapper.unmount()
  })

  it('domain: D opens a cancellable confirmation and empty note confirms', async () => {
    const declined = stubApi({
      candidateCount: 1,
      details: { 1: candidateDetails(1, { reviewStatus: 'shortlisted' }) },
    })
    const declinedView = await mountReview()
    await flushPromises()

    window.dispatchEvent(new KeyboardEvent('keydown', { key: 'd', shiftKey: true }))
    await flushPromises()
    expect(declined.requests.filter((request) => request.url.endsWith('/outcome'))).toHaveLength(0)
    expect(document.body.querySelector('[role="dialog"]')?.textContent).toContain(
      'Does not reopen a slot',
    )
    window.dispatchEvent(new KeyboardEvent('keydown', { key: 'Escape' }))
    await flushPromises()
    expect(document.body.querySelector('[role="dialog"]')).toBeNull()
    expect(declined.requests.filter((request) => request.url.endsWith('/outcome'))).toHaveLength(0)

    window.dispatchEvent(new KeyboardEvent('keydown', { key: 'd' }))
    await flushPromises()
    expect(document.body.querySelector('[role="dialog"]')).toBeNull()

    window.dispatchEvent(new KeyboardEvent('keydown', { key: 'd', shiftKey: true }))
    await flushPromises()
    const declinedNote = document.body.querySelector('textarea[aria-label="Outcome note"]')
    expect(declinedNote).toBeDefined()
    declinedNote?.dispatchEvent(new KeyboardEvent('keydown', { key: 'Enter', bubbles: true }))
    await flushPromises()
    expect(declined.requests.filter((request) => request.url.endsWith('/outcome')).at(-1)?.body).toEqual({
      outcome: 'declined',
    })
    declinedView.wrapper.unmount()
  })

  it('US-18: HR explicitly saves notes and sees a Saving… to Saved whisper', async () => {
    let resolveNotes: ((response: Response) => void) | undefined
    const notesGate = new Promise<Response>((resolve) => {
      resolveNotes = resolve
    })
    const fetchMock = vi.fn((input: RequestInfo | URL, init?: RequestInit) => {
      const url = String(input)
      if (init?.method === 'PUT' && url.endsWith('/notes')) {
        return notesGate
      }
      if (/\/candidates\/\d+$/.test(url)) {
        return Promise.resolve(jsonResponse(candidateDetails(1)))
      }
      if (url.includes('/review-queue')) {
        return Promise.resolve(jsonResponse([candidateSummary(1), candidateSummary(2)]))
      }
      if (url.endsWith('/vacancies/1')) {
        return Promise.resolve(jsonResponse(vacancyDetails()))
      }
      throw new Error(`Unstubbed fetch: ${init?.method ?? 'GET'} ${url}`)
    })
    vi.stubGlobal('fetch', fetchMock)

    const { wrapper } = await mountReview()
    await flushPromises()

    const notes = wrapper.find('textarea[aria-label="Candidate notes"]')
    await notes.setValue('Call back about SAP')
    expect(
      fetchMock.mock.calls.filter(
        ([input, init]) => init?.method === 'PUT' && String(input).endsWith('/notes'),
      ),
    ).toHaveLength(0)

    await findButton(wrapper, 'Save notes').trigger('click')

    expect(wrapper.text()).toContain('Saving…')
    resolveNotes!(jsonResponse(candidateDetails(1, { notes: 'Call back about SAP' })))
    await flushPromises()

    expect(wrapper.text()).toContain('Saved')
    wrapper.unmount()
  })

  it('US-17: HR saves a manual requirement confirmation beside the candidate', async () => {
    const { requests } = stubApi()
    const { wrapper } = await mountReview()
    await flushPromises()

    const checkbox = wrapper.find('input[aria-label="Confirm VAT reporting requirement"]')
    await checkbox.setValue(true)
    await flushPromises()

    const requirementRequest = requests.find((request) =>
      request.url.endsWith('/requirement-reviews/12'),
    )
    expect(requirementRequest?.body).toEqual({ confirmed: true })
    expect(wrapper.text()).toContain('2 / 3 confirmed')
    wrapper.unmount()
  })

  it('US-17: HR toggles manual requirements with numbered shortcuts', async () => {
    const { requests } = stubApi()
    const { wrapper } = await mountReview()
    await flushPromises()

    const requirements = wrapper.find('[aria-label="Manual requirement review"]')
    expect(requirements.findAll('kbd').map((kbd) => kbd.text())).toEqual(['1', '2', '3'])
    expect(
      requirements
        .find('input[aria-label="Confirm Excel requirement"]')
        .attributes('aria-keyshortcuts'),
    ).toBe('1')
    expect(
      requirements
        .find('input[aria-label="Confirm VAT reporting requirement"]')
        .attributes('aria-keyshortcuts'),
    ).toBe('2')

    window.dispatchEvent(new KeyboardEvent('keydown', { key: '1' }))
    await flushPromises()

    const firstRequirementRequest = requests.find((request) =>
      request.url.endsWith('/requirement-reviews/11'),
    )
    expect(firstRequirementRequest?.body).toEqual({ confirmed: false })

    window.dispatchEvent(new KeyboardEvent('keydown', { key: '2' }))
    await flushPromises()

    const secondRequirementRequest = requests.find((request) =>
      request.url.endsWith('/requirement-reviews/12'),
    )
    expect(secondRequirementRequest?.body).toEqual({ confirmed: true })
    wrapper.unmount()
  })

  it('US-17: HR edits the manually entered candidate details', async () => {
    const { requests } = stubApi({
      details: { 1: candidateDetails(1, { fullName: null, contactEmail: null }) },
    })
    const { wrapper } = await mountReview()
    await flushPromises()

    await findButton(wrapper, 'Edit').trigger('click')
    await wrapper.find('input[aria-label="Candidate name"]').setValue('Jane Updated')
    await wrapper.find('input[aria-label="Candidate email"]').setValue('jane.updated@mail.com')
    await wrapper.find('form').trigger('submit')
    await flushPromises()

    const detailsRequest = requests.find((request) => request.url.endsWith('/candidates/1/details'))
    expect(detailsRequest?.body).toEqual({
      fullName: 'Jane Updated',
      contactEmail: 'jane.updated@mail.com',
    })
    expect(wrapper.text()).toContain('Jane Updated')
    expect(wrapper.text()).toContain('jane.updated@mail.com')
    wrapper.unmount()
  })

  it('US-17: Prev is disabled on the first candidate, Next on the last, and deciding on the last stays put', async () => {
    const { requests } = stubApi()
    const { wrapper, router } = await mountReview()
    await flushPromises()

    expect(findButton(wrapper, 'Prev').attributes('disabled')).toBeDefined()
    expect(findButton(wrapper, 'Next').attributes('disabled')).toBeUndefined()

    await findButton(wrapper, 'Next').trigger('click')
    await flushPromises()

    expect(router.currentRoute.value.params.candidateId).toBe('2')
    expect(findButton(wrapper, 'Prev').attributes('disabled')).toBeUndefined()
    expect(findButton(wrapper, 'Next').attributes('disabled')).toBeDefined()

    await findButton(wrapper, 'Flag').trigger('click')
    await flushPromises()

    const flagRequest = requests.find((request) =>
      request.url.endsWith('/candidates/2/review'),
    )
    expect(flagRequest?.body).toMatchObject({ reviewStatus: 'flagged' })
    // Nowhere to advance: HR stays on the last candidate.
    expect(router.currentRoute.value.params.candidateId).toBe('2')
    wrapper.unmount()
  })

  it('US-17: an email without a PDF remains reviewable with its source email', async () => {
    stubApi({ details: { 1: candidateDetails(1, { documents: [] }) } })
    const { wrapper } = await mountReview()
    await flushPromises()

    expect(wrapper.text()).toContain('This candidate has no CV document.')
    await findButton(wrapper, 'Open').trigger('click')
    const dialog = document.body.querySelector('[role="dialog"]')
    expect(dialog).not.toBeNull()
    expect(dialog?.textContent).toContain('Please find my CV attached.')
    wrapper.unmount()
  })

  it('domain: a closed round keeps the review workspace readable but read-only', async () => {
    stubApi({
      vacancy: vacancyDetails({
        rounds: [openRound({ status: 'closed', closedAt: '2026-09-05T00:00:00Z' })],
      }),
    })
    const { wrapper } = await mountReview()
    await flushPromises()

    // The read-only notice sits above the still-readable workspace.
    expect(wrapper.text()).toContain('This round is closed')
    expect(wrapper.text()).toContain('Review data is read-only')
    expect(wrapper.text()).toContain('Jane Doe')
    expect(wrapper.text()).toContain('1 / 3 confirmed')
    wrapper.unmount()
  })

  it.each([
    { action: 'Shortlist', status: 'shortlisted' },
    { action: 'Flag', status: 'flagged' },
    { action: 'Reject', status: 'rejected' },
  ] as const)(
    'domain: when the round closes mid-review, a refused $status decision surfaces the conflict',
    async ({ action }) => {
      stubApi({
        mutationError: {
          path: '/review',
          problem: { title: 'IntakeRounds.Closed', detail: 'The round is closed.' },
          status: 409,
        },
      })

      const { wrapper, router } = await mountReview()
      await flushPromises()

      await findButton(wrapper, action).trigger('click')
      await flushPromises()

      // The server enforces the freeze: HR stays on the candidate and gets an amber warning.
      expect(toastAdd).toHaveBeenCalledWith(
        expect.objectContaining({
          title: 'This round is closed',
          description:
            'Closed rounds are read-only. To keep working with its candidates, promote them into the active round.',
          color: 'warning',
        }),
      )
      expect(router.currentRoute.value.params.candidateId).toBe('1')
      expect(wrapper.find('p[role="alert"]').exists()).toBe(false)
      wrapper.unmount()
    },
  )

  it('domain: a closed vacancy settles the outcome in the header and hides outcome actions', async () => {
    stubApi({
      vacancy: vacancyDetails({ status: 'closed', closedAt: '2026-09-06T00:00:00Z' }),
      details: { 1: candidateDetails(1, { reviewStatus: 'shortlisted' }) },
    })
    const { wrapper } = await mountReview()
    await flushPromises()

    expect(wrapper.find('[aria-label="Review status: Shortlisted"]').exists()).toBe(true)
    expect(wrapper.find('[aria-label="Hire outcomes"]').exists()).toBe(false)
    expect(hasButton(wrapper, 'Shortlist')).toBe(false)
    wrapper.unmount()
  })

  it.each([
    { action: 'Shortlist', status: 'shortlisted' },
    { action: 'Reject', status: 'rejected' },
  ] as const)(
    'domain: a $status decision against a final hire outcome toasts the server rule',
    async ({ action }) => {
      stubApi({
        mutationError: {
          path: '/review',
          problem: {
            type: 'https://tools.ietf.org/html/rfc9110#section-15.5.1',
            title: 'One or more validation errors occurred.',
            status: 400,
            errors: {
              reviewStatus: [
                'Review status cannot change while the hire outcome is hired or runaway.',
              ],
            },
            traceId: '00-a2de65ca9fb16c00d2070d62c0f7cd9d-bb2c5cd40d4cf8f2-00',
          },
          status: 400,
        },
      })

      const { wrapper, router } = await mountReview()
      await flushPromises()

      await findButton(wrapper, action).trigger('click')
      await flushPromises()

      // The server's rule arrives verbatim as an amber toast; HR stays on the candidate.
      expect(toastAdd).toHaveBeenCalledWith(
        expect.objectContaining({
          title: "That change isn't allowed",
          description:
            'Review status cannot change while the hire outcome is hired or runaway.',
          color: 'warning',
        }),
      )
      expect(router.currentRoute.value.params.candidateId).toBe('1')
      expect(wrapper.find('p[role="alert"]').exists()).toBe(false)
      wrapper.unmount()
    },
  )

  it('domain: a refused hire-outcome transition toasts the server rule', async () => {
    stubApi({
      details: {
        1: candidateDetails(1, { reviewStatus: 'shortlisted', hireOutcome: 'hired' }),
      },
      mutationError: {
        path: '/outcome',
        problem: {
          title: 'One or more validation errors occurred.',
          errors: { hireOutcome: ['The requested hire outcome transition is not allowed.'] },
        },
        status: 400,
      },
    })
    const { wrapper, router } = await mountReview()
    await flushPromises()

    await openOutcomeMenu(wrapper)
    await findMenuItem('Clear outcome').trigger('click')
    await flushPromises()

    expect(toastAdd).toHaveBeenCalledWith(
      expect.objectContaining({
        title: "That change isn't allowed",
        description: 'The requested hire outcome transition is not allowed.',
        color: 'warning',
      }),
    )
    expect(wrapper.find('p[role="alert"]').exists()).toBe(false)
    expect(router.currentRoute.value.params.candidateId).toBe('1')
    wrapper.unmount()
  })

  it('domain: a technical review-save failure stays inline with retry guidance', async () => {
    stubApi({
      mutationError: {
        path: '/review',
        problem: { title: 'Server error' },
        status: 500,
      },
    })
    const { wrapper, router } = await mountReview()
    await flushPromises()

    await findButton(wrapper, 'Reject').trigger('click')
    await flushPromises()

    const alert = wrapper.find('p[role="alert"]')
    expect(alert.text()).toContain('Something went wrong')
    expect(alert.text()).toContain('Please try again.')
    expect(router.currentRoute.value.params.candidateId).toBe('1')
    expect(toastAdd).not.toHaveBeenCalled()
    wrapper.unmount()
  })

  it('domain: an unknown review conflict explains how to refresh the round', async () => {
    stubApi({
      mutationError: {
        path: '/review',
        problem: { title: 'Conflict' },
        status: 409,
      },
    })
    const { wrapper, router } = await mountReview()
    await flushPromises()

    await findButton(wrapper, 'Reject').trigger('click')
    await flushPromises()

    const alert = wrapper.find('p[role="alert"]')
    expect(alert.text()).toContain("Couldn't save that change")
    expect(alert.text()).toContain(
      'Go back and reopen this round to see the latest, then try again.',
    )
    expect(router.currentRoute.value.params.candidateId).toBe('1')
    expect(toastAdd).not.toHaveBeenCalled()
    wrapper.unmount()
  })

  // Issue 04: the workspace branches by Intake Source — a form-sourced
  // candidate shows its Form Response as the dominant evidence.
  describe('form-evidence variant', () => {
    it('US-17: a form candidate renders the Form Answers panel instead of the CV viewer and source email', async () => {
      const { requests } = stubApi({
        details: { 1: formCandidateDetails(1) },
        formLayout: formLayoutDto(),
      })
      const { wrapper } = await mountReview()
      await flushPromises()

      const panel = wrapper.find('[aria-label="Form answers"]')
      expect(panel.exists()).toBe(true)
      // Every other Picked Column is a Form Answer (CONTEXT.md): role-less
      // columns only, in column order; the label leads and the header snapshot
      // shows muted beside it when both exist.
      const rows = panel
        .findAll('dt')
        .map((row) => row.text().replace(/\s+/g, ' ').trim())
      expect(rows).toEqual(['4 · Experience · Pengalaman kerja', '5 · Posisi'])
      const values = panel.findAll('dd')
      expect(values[0]?.text()).toContain('3 years, PT X')
      expect(values[0]?.text()).toContain('Shift rotation')
      expect(values[1]?.text()).toBe('Operator produksi')

      expect(wrapper.find('[aria-label="CV viewer"]').exists()).toBe(false)
      expect(wrapper.find('[aria-label="Source email"]').exists()).toBe(false)
      // The layout is vacancy-level configuration: fetched once, lazily.
      expect(requests.filter((request) => request.url.endsWith('/form-layout'))).toHaveLength(1)
      wrapper.unmount()
    })

    it('US-17: the header names the evidence source and links the stored CV in a new tab', async () => {
      stubApi({
        details: { 1: formCandidateDetails(1) },
        formLayout: formLayoutDto(),
      })
      const { wrapper } = await mountReview()
      await flushPromises()

      expect(wrapper.text()).toContain('Form Response')
      expect(wrapper.text()).toContain(
        `Submitted ${formatReceivedAt('2026-09-12T09:58:00Z')}`,
      )

      const openCv = wrapper.find('a[href="https://drive.example.test/cv/siti"]')
      expect(openCv.exists()).toBe(true)
      expect(openCv.attributes('target')).toBe('_blank')
      expect(openCv.attributes('rel')).toContain('noopener')
      expect(openCv.attributes('aria-keyshortcuts')).toBe('C')
      expect(openCv.text()).toContain('Open CV')
      wrapper.unmount()
    })

    it('US-17: C opens the stored CV link, and typing c inside Notes does not', async () => {
      const windowOpen = vi.spyOn(window, 'open').mockImplementation(() => null)
      stubApi({
        details: { 1: formCandidateDetails(1) },
        formLayout: formLayoutDto(),
      })
      const { wrapper } = await mountReview()
      await flushPromises()

      const notes = wrapper.find('textarea[aria-label="Candidate notes"]')
      notes.element.dispatchEvent(new KeyboardEvent('keydown', { key: 'c', bubbles: true }))
      await flushPromises()
      expect(windowOpen).not.toHaveBeenCalled()

      window.dispatchEvent(new KeyboardEvent('keydown', { key: 'c' }))
      await flushPromises()
      expect(windowOpen).toHaveBeenCalledWith(
        'https://drive.example.test/cv/siti',
        '_blank',
        'noopener',
      )
      windowOpen.mockRestore()
      wrapper.unmount()
    })

    it('US-17: an empty CV link cell renders a disabled "No CV link" button and C stays inert', async () => {
      const windowOpen = vi.spyOn(window, 'open').mockImplementation(() => null)
      stubApi({
        details: {
          1: formCandidateDetails(1, {
            formResponses: [
              formResponse(['2026-09-12T09:58:00Z', 'Joko', 'joko@example.com', '   ', '2 years', 'Operator']),
            ],
          }),
        },
        formLayout: formLayoutDto(),
      })
      const { wrapper } = await mountReview()
      await flushPromises()

      // The gap is never hidden: a disabled button with gap copy stands in.
      const noCvLink = wrapper
        .findAll('button')
        .find((button) => button.text().includes('No CV link'))
      expect(noCvLink, 'a disabled "No CV link" button').toBeDefined()
      expect(noCvLink?.attributes('disabled')).toBeDefined()
      expect(wrapper.find('a[aria-keyshortcuts="C"]').exists()).toBe(false)

      window.dispatchEvent(new KeyboardEvent('keydown', { key: 'c' }))
      await flushPromises()
      expect(windowOpen).not.toHaveBeenCalled()
      windowOpen.mockRestore()
      wrapper.unmount()
    })

    it('domain: a Resubmitted candidate is flagged in the review header', async () => {
      stubApi({
        details: { 1: formCandidateDetails(1, { isResubmitted: true }) },
        formLayout: formLayoutDto(),
      })
      const { wrapper } = await mountReview()
      await flushPromises()

      expect(wrapper.text()).toContain('Resubmitted')
      expect(wrapper.text()).toContain('Form Response')
      wrapper.unmount()
    })

    it('US-17: the email variant keeps the CV viewer and source email and never fetches a form layout', async () => {
      const { requests } = stubApi()
      const { wrapper } = await mountReview()
      await flushPromises()

      expect(wrapper.find('[aria-label="CV viewer"]').exists()).toBe(true)
      expect(wrapper.find('[aria-label="Source email"]').exists()).toBe(true)
      expect(wrapper.find('[aria-label="Form answers"]').exists()).toBe(false)
      expect(wrapper.find('a[aria-keyshortcuts="C"]').exists()).toBe(false)
      expect(wrapper.text()).toContain('Source Email')
      expect(wrapper.text()).not.toContain('Submitted')
      expect(wrapper.text()).not.toContain('Form Response')
      // The stub throws on any unstubbed fetch; reaching this assertion
      // already proves no layout request fired — kept explicit as a tripwire.
      expect(requests.some((request) => request.url.endsWith('/form-layout'))).toBe(false)
      wrapper.unmount()
    })

    it('US-17: decision shortcuts and auto-advance behave identically on the form variant', async () => {
      const { requests } = stubApi({
        candidateCount: 3,
        details: { 1: formCandidateDetails(1) },
        formLayout: formLayoutDto(),
      })
      const { wrapper, router } = await mountReview()
      await flushPromises()

      window.dispatchEvent(new KeyboardEvent('keydown', { key: 's' }))
      await flushPromises()
      expect(router.currentRoute.value.params.candidateId).toBe('2')

      window.dispatchEvent(new KeyboardEvent('keydown', { key: 'f' }))
      await flushPromises()
      expect(router.currentRoute.value.params.candidateId).toBe('3')

      // Nowhere to advance: deciding on the last candidate stays put.
      window.dispatchEvent(new KeyboardEvent('keydown', { key: 'r' }))
      await flushPromises()
      expect(router.currentRoute.value.params.candidateId).toBe('3')

      const decisions = requests.filter(
        (request) => request.method === 'PUT' && request.url.endsWith('/review'),
      )
      expect(decisions.map((request) => request.body?.reviewStatus)).toEqual([
        'shortlisted',
        'flagged',
        'rejected',
      ])
      wrapper.unmount()
    })

    it('domain: a form candidate whose vacancy has no layout sees the panel empty state, never an error', async () => {
      stubApi({
        details: { 1: formCandidateDetails(1) },
        formLayoutError: {
          problem: { title: 'FormLayouts.NotFound', detail: 'No form layout.' },
          status: 404,
        },
      })
      const { wrapper } = await mountReview()
      await flushPromises()

      const panel = wrapper.find('[aria-label="Form answers"]')
      expect(panel.exists()).toBe(true)
      expect(panel.text()).toContain('This response has no picked columns to show.')
      expect(panel.find('[role="alert"]').exists()).toBe(false)
      // No layout, no CV link: the header falls back to the visible gap state,
      // and the form Timestamp (system data) still shows.
      expect(hasButton(wrapper, 'No CV link')).toBe(true)
      expect(wrapper.text()).toContain(
        `Submitted ${formatReceivedAt('2026-09-12T09:58:00Z')}`,
      )
      wrapper.unmount()
    })

    it('domain: a failed layout fetch surfaces an inline error and Try again refetches', async () => {
      let layoutCalls = 0
      const fetchMock = vi.fn((input: RequestInfo | URL, init?: RequestInit) => {
        const url = String(input)
        if (init?.method === undefined && url.endsWith('/form-layout')) {
          layoutCalls += 1
          return layoutCalls === 1
            ? Promise.resolve(jsonResponse({ title: 'Server error' }, 500))
            : Promise.resolve(jsonResponse(formLayoutDto()))
        }
        if (/\/candidates\/\d+$/.test(url)) {
          return Promise.resolve(jsonResponse(formCandidateDetails(1)))
        }
        if (url.includes('/review-queue')) {
          return Promise.resolve(jsonResponse([candidateSummary(1), candidateSummary(2)]))
        }
        if (url.endsWith('/vacancies/1')) {
          return Promise.resolve(jsonResponse(vacancyDetails()))
        }
        throw new Error(`Unstubbed fetch: ${init?.method ?? 'GET'} ${url}`)
      })
      vi.stubGlobal('fetch', fetchMock)

      const { wrapper } = await mountReview()
      await flushPromises()

      const alert = wrapper.find('[aria-label="Form answers"] [role="alert"]')
      expect(alert.exists()).toBe(true)
      expect(alert.text()).toContain("Couldn't load the form answers")

      await findButton(wrapper, 'Try again').trigger('click')
      await flushPromises()

      expect(layoutCalls).toBe(2)
      const panel = wrapper.find('[aria-label="Form answers"]')
      expect(panel.find('[role="alert"]').exists()).toBe(false)
      expect(panel.text()).toContain('Experience')
      expect(panel.text()).toContain('Operator produksi')
      wrapper.unmount()
    })
  })
})
