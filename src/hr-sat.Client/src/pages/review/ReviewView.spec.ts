import { DOMWrapper, flushPromises, mount, type VueWrapper } from '@vue/test-utils'
import { createMemoryHistory, createRouter, type Router } from 'vue-router'
import { afterEach, describe, expect, it, vi } from 'vitest'

import ReviewView from './ReviewView.vue'

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
    template: '<div data-testid="pdf-page" />',
  },
}))

function jsonResponse(body: unknown, status = 200): Response {
  return new Response(JSON.stringify(body), {
    status,
    headers: { 'Content-Type': 'application/json' },
  })
}

function vacancyDetails() {
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
    progress: { processedCandidates: 0, totalCandidates: 2 },
  }
}

const candidateNames = ['Jane Doe', 'Bob Builder', 'Ann Lee']

function candidateSummary(id: number) {
  const name = candidateNames[id - 1] ?? `Candidate ${id}`
  return {
    id,
    fullName: name,
    contactEmail: `candidate${id}@mail.com`,
    contactPhone: null,
    notes: null,
    reviewStatus: 'new',
    sourceSenderName: name,
    sourceSenderEmail: `candidate${id}@mail.com`,
    sourceSubject: `${name} application`,
    sourceSentAt: '2026-08-10T09:00:00Z',
  }
}

function candidateDetails(id: number, overrides: Record<string, unknown> = {}) {
  const name = candidateNames[id - 1] ?? `Candidate ${id}`
  return {
    id,
    reviewStatus: 'new',
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
    documents: [
      {
        id: id * 10,
        originalFilename: `cv-${id}.pdf`,
        sizeBytes: 2048,
        isPrimary: true,
        downloadUrl: `/api/vacancies/1/candidates/${id}/cv-documents/${id * 10}`,
      },
    ],
    ...overrides,
  }
}

interface CapturedRequest {
  url: string
  method: string
  body: Record<string, unknown> | undefined
}

function stubApi(
  options: { candidateCount?: number; details?: Record<number, Record<string, unknown>> } = {},
) {
  const count = options.candidateCount ?? 2
  const requests: CapturedRequest[] = []
  const mock = vi.fn((input: RequestInfo | URL, init?: RequestInit) => {
    const url = String(input)
    const method = init?.method ?? 'GET'
    const body = typeof init?.body === 'string' ? JSON.parse(init.body) : undefined
    requests.push({ url, method, body })

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
        jsonResponse(candidateDetails(id, { reviewStatus: body?.reviewStatus, notes: body?.notes })),
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
    if (method === 'GET' && url.endsWith('/candidates')) {
      return Promise.resolve(
        jsonResponse(Array.from({ length: count }, (_, index) => candidateSummary(index + 1))),
      )
    }
    return Promise.resolve(jsonResponse(vacancyDetails()))
  })
  vi.stubGlobal('fetch', mock)
  return { requests }
}

const Harness = { template: '<router-view />' }

async function mountReview(startCandidateId = '1'): Promise<{
  wrapper: VueWrapper
  router: Router
}> {
  const router = createRouter({
    history: createMemoryHistory(),
    routes: [
      { path: '/vacancies/:id', name: 'vacancy-detail', component: { template: '<div />' } },
      {
        path: '/vacancies/:id/review/:candidateId',
        name: 'candidate-review',
        component: ReviewView,
        props: true,
      },
    ],
  })
  await router.push(`/vacancies/1/review/${startCandidateId}`)
  const wrapper = mount(Harness, { global: { plugins: [router] } })
  return { wrapper, router }
}

function findButton(wrapper: VueWrapper, label: string): DOMWrapper<HTMLButtonElement> {
  const button = wrapper
    .findAll('button')
    .find((candidate) => candidate.text().includes(label))
  expect(button, `a "${label}" button`).toBeDefined()
  return button as unknown as DOMWrapper<HTMLButtonElement>
}

afterEach(() => {
  vi.clearAllMocks()
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
    wrapper.unmount()
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
      if (url.endsWith('/candidates')) {
        return Promise.resolve(jsonResponse([candidateSummary(1), candidateSummary(2)]))
      }
      return Promise.resolve(jsonResponse(vacancyDetails()))
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
    expect(wrapper.text()).toContain('Please find my CV attached.')
    wrapper.unmount()
  })
})
