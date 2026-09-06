import { DOMWrapper, flushPromises, mount, type VueWrapper } from '@vue/test-utils'
import { createMemoryHistory, createRouter, type Router } from 'vue-router'
import { afterEach, describe, expect, it, vi } from 'vitest'

import ReviewView from './ReviewView.vue'

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
        downloadUrl: `/api/vacancies/1/rounds/1/candidates/${id}/cv-documents/${id * 10}`,
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
  options: {
    candidateCount?: number
    details?: Record<number, Record<string, unknown>>
    vacancy?: Record<string, unknown>
  } = {},
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
        jsonResponse(
          candidateDetails(id, {
            ...options.details?.[id],
            reviewStatus: body?.reviewStatus,
            notes: body?.notes,
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
    if (method === 'GET' && url.endsWith('/candidates')) {
      return Promise.resolve(
        jsonResponse(Array.from({ length: count }, (_, index) => candidateSummary(index + 1))),
      )
    }
    if (method === 'GET' && url.endsWith('/vacancies/1')) {
      return Promise.resolve(jsonResponse(options.vacancy ?? vacancyDetails()))
    }
    throw new Error(`Unstubbed fetch: ${method} ${url}`)
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
        path: '/vacancies/:id/rounds/:roundId/review/:candidateId',
        name: 'candidate-review',
        component: ReviewView,
        props: true,
      },
    ],
  })
  await router.push(`/vacancies/1/rounds/1/review/${startCandidateId}`)
  const wrapper = mount(Harness, { attachTo: document.body, global: { plugins: [router] } })
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
    expect(wrapper.find('button[aria-label="Previous page"] kbd').text()).toBe('Shift+←')
    expect(wrapper.find('button[aria-label="Next page"] kbd').text()).toBe('Shift+→')
    wrapper.unmount()
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

    const shortcutButton = wrapper.find('button[aria-label="Keyboard shortcuts"]')
    expect(shortcutButton.exists()).toBe(true)
    expect(shortcutButton.attributes('aria-keyshortcuts')).toBe('?')
    expect(shortcutButton.text()).toContain('Shortcuts')
    expect(shortcutButton.text()).toContain('?')

    await shortcutButton.trigger('click')
    await flushPromises()
    expect(document.body.querySelector('[role="dialog"]')?.textContent).toContain(
      'Keyboard shortcuts',
    )
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

  it('domain: when the round closes mid-review, a refused decision surfaces the conflict', async () => {
    const fetchMock = vi.fn((input: RequestInfo | URL, init?: RequestInit) => {
      const url = String(input)
      const method = init?.method ?? 'GET'
      if (method === 'PUT' && url.endsWith('/candidates/1/review')) {
        return Promise.resolve(
          jsonResponse({ title: 'Conflict', detail: 'The round is closed.' }, 409),
        )
      }
      if (method === 'GET' && /\/candidates\/\d+$/.test(url)) {
        return Promise.resolve(jsonResponse(candidateDetails(1)))
      }
      if (method === 'GET' && url.endsWith('/candidates')) {
        return Promise.resolve(jsonResponse([candidateSummary(1), candidateSummary(2)]))
      }
      if (method === 'GET' && url.endsWith('/vacancies/1')) {
        return Promise.resolve(jsonResponse(vacancyDetails()))
      }
      throw new Error(`Unstubbed fetch: ${method} ${url}`)
    })
    vi.stubGlobal('fetch', fetchMock)

    const { wrapper, router } = await mountReview()
    await flushPromises()

    await findButton(wrapper, 'Reject').trigger('click')
    await flushPromises()

    // The server enforces the freeze: HR stays on the candidate and sees why.
    expect(wrapper.find('p[role="alert"]').text()).toContain('API request failed with status 409')
    expect(router.currentRoute.value.params.candidateId).toBe('1')
    expect(toastAdd).not.toHaveBeenCalled()
    wrapper.unmount()
  })
})
