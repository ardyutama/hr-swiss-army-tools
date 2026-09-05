import { DOMWrapper, flushPromises, mount, type VueWrapper } from '@vue/test-utils'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { createMemoryHistory, createRouter } from 'vue-router'

import VacancyDetailView from './VacancyDetailView.vue'

const toastAdd = vi.fn()

vi.mock('@nuxt/ui/composables/useToast', () => ({
  useToast: () => ({ add: toastAdd }),
}))

function jsonResponse(body: unknown, status = 200): Response {
  return new Response(JSON.stringify(body), {
    status,
    headers: { 'Content-Type': 'application/json' },
  })
}

function vacancyDetails(overrides: Partial<Record<string, unknown>> = {}) {
  return {
    id: 1,
    title: 'Welder',
    openedOn: '2026-08-27',
    status: 'open',
    closedAt: null,
    createdAt: '2026-08-27T00:00:00Z',
    requirements: [{ id: 11, phrase: 'MIG welding', position: 0 }],
    progress: { processedCandidates: 0, totalCandidates: 0 },
    ...overrides,
  }
}

function importedCandidate(id: number, filename: string) {
  return {
    id,
    reviewStatus: 'new',
    extractionStatus: 'pending',
    sourceSenderName: 'Alice Applicant',
    sourceSenderEmail: 'alice@example.com',
    sourceSubject: 'Alice application',
    sourceBodyText: 'Please find my CV attached.',
    sourceSentAt: '2026-08-28T09:00:00Z',
    sourceOriginalFilename: filename,
    documents: [
      {
        id: id * 10,
        originalFilename: 'alice-cv.pdf',
        sizeBytes: 1024,
        isPrimary: true,
        downloadUrl: `/api/vacancies/1/candidates/${id}/cv-documents/${id * 10}`,
      },
    ],
  }
}

function candidateSummary(id: number, overrides: Partial<Record<string, unknown>> = {}) {
  return {
    id,
    fullName: null,
    contactEmail: null,
    notes: null,
    reviewStatus: 'new',
    sourceSenderName: 'Alice Applicant',
    sourceSenderEmail: 'alice@example.com',
    sourceSubject: 'Application for Welder',
    sourceSentAt: '2026-08-28T09:00:00Z',
    cvDocumentCount: 1,
    ...overrides,
  }
}

function bobSummary() {
  return candidateSummary(2, {
    sourceSenderName: 'Bob Builder',
    sourceSenderEmail: 'bob@example.com',
    sourceSubject: 'Bob application',
    reviewStatus: 'shortlisted',
    notes: 'Strong MIG experience',
  })
}

function mountView(id = '1') {
  // A memory-history router with a stub review route stands in for the real router;
  // the candidate-review route itself is wired by ticket 05.
  const router = createRouter({
    history: createMemoryHistory(),
    routes: [
      { path: '/', name: 'vacancy-list', component: { template: '<div />' } },
      { path: '/vacancies/:id', name: 'vacancy-detail', component: { template: '<div />' } },
      {
        path: '/vacancies/:id/review/:candidateId',
        name: 'candidate-review',
        component: { template: '<div />' },
      },
    ],
  })
  const wrapper = mount(VacancyDetailView, {
    props: { id },
    global: {
      plugins: [router],
      stubs: {
        RouterLink: { template: '<a><slot /></a>' },
      },
    },
  })
  return { router, wrapper }
}

function bodyElement(selector: string): Element {
  const element = document.body.querySelector(selector)
  if (!element) {
    throw new Error(`Expected "${selector}" to exist in document.body`)
  }
  return element
}

function dialogButton(text: string): HTMLButtonElement | undefined {
  return Array.from(document.body.querySelectorAll('button')).find((button) =>
    button.textContent?.includes(text),
  )
}

async function openImportDialog(wrapper: VueWrapper) {
  const button = wrapper
    .findAll('button')
    .find((candidate) => candidate.text().includes('Import .eml'))
  expect(button, 'an Import .eml button').toBeDefined()
  await button!.trigger('click')
  await flushPromises()
}

function dropFiles(files: File[]) {
  return new DOMWrapper(bodyElement('.dropzone')).trigger('drop', { dataTransfer: { files } })
}

afterEach(() => {
  vi.clearAllMocks()
  vi.unstubAllGlobals()
  document.body.innerHTML = ''
})

describe('VacancyDetailView', () => {
  it('US-12: HR drops .eml files into a vacancy and sees each file’s outcome', async () => {
    let imported = false
    let sentFileNames: string[] | undefined
    const fetchMock = vi.fn((input: RequestInfo | URL, init?: RequestInit) => {
      const url = String(input)
      if (init?.method === 'POST' && url.endsWith('/candidates/import')) {
        imported = true
        const form = init.body as FormData
        sentFileNames = form.getAll('files').map((entry) => (entry as File).name)
        return Promise.resolve(
          jsonResponse({
            results: [
              {
                fileName: 'alice.eml',
                status: 'imported',
                error: null,
                candidate: importedCandidate(1, 'alice.eml'),
              },
              {
                fileName: 'broken.eml',
                status: 'failed',
                error: 'The .eml file must contain at least one valid PDF attachment.',
                candidate: null,
              },
            ],
          }),
        )
      }
      if (url.endsWith('/candidates')) {
        return Promise.resolve(jsonResponse(imported ? [candidateSummary(1)] : []))
      }
      return Promise.resolve(
        jsonResponse(
          vacancyDetails({
            progress: imported
              ? { processedCandidates: 0, totalCandidates: 1 }
              : { processedCandidates: 0, totalCandidates: 0 },
          }),
        ),
      )
    })
    vi.stubGlobal('fetch', fetchMock)

    const { wrapper } = mountView()
    await flushPromises()
    expect(wrapper.text()).toContain('Welder')

    await openImportDialog(wrapper)
    await dropFiles([
      new File(['alice source'], 'alice.eml', { type: 'message/rfc822' }),
      new File(['broken source'], 'broken.eml', { type: 'message/rfc822' }),
    ])
    await flushPromises()

    // Every dropped file is submitted as a repeated multipart `files` entry.
    expect(sentFileNames).toEqual(['alice.eml', 'broken.eml'])

    // Each file's outcome is visible to HR.
    expect(wrapper.text()).toContain('alice.eml')
    expect(wrapper.text()).toContain('Imported')
    expect(wrapper.text()).toContain('broken.eml')
    expect(wrapper.text()).toContain('Failed')
    expect(wrapper.text()).toContain('at least one valid PDF attachment')
    expect(toastAdd).toHaveBeenCalledWith(
      expect.objectContaining({
        title: 'Import complete: 1 imported, 1 failed',
        color: 'success',
      }),
    )
    wrapper.unmount()
  })

  it('US-13: the vacancy progress reflects the imported candidates', async () => {
    let imported = false
    const fetchMock = vi.fn((input: RequestInfo | URL, init?: RequestInit) => {
      const url = String(input)
      if (init?.method === 'POST' && url.endsWith('/candidates/import')) {
        imported = true
        return Promise.resolve(
          jsonResponse({
            results: [
              {
                fileName: 'alice.eml',
                status: 'imported',
                error: null,
                candidate: importedCandidate(1, 'alice.eml'),
              },
            ],
          }),
        )
      }
      if (url.endsWith('/candidates')) {
        return Promise.resolve(jsonResponse(imported ? [candidateSummary(1)] : []))
      }
      return Promise.resolve(
        jsonResponse(
          vacancyDetails({
            progress: imported
              ? { processedCandidates: 0, totalCandidates: 1 }
              : { processedCandidates: 0, totalCandidates: 0 },
          }),
        ),
      )
    })
    vi.stubGlobal('fetch', fetchMock)

    const { wrapper } = mountView()
    await flushPromises()

    const progress = () => wrapper.find('[aria-label="Vacancy progress"]').text()
    expect(progress()).toContain('0/0')

    await openImportDialog(wrapper)
    await dropFiles([new File(['alice source'], 'alice.eml', { type: 'message/rfc822' })])
    await flushPromises()

    expect(progress()).toContain('0/1')
    wrapper.unmount()
  })

  it('US-12: HR is told when the import request fails', async () => {
    const fetchMock = vi.fn((input: RequestInfo | URL, init?: RequestInit) => {
      const url = String(input)
      if (init?.method === 'POST' && url.endsWith('/candidates/import')) {
        return Promise.resolve(
          jsonResponse(
            { errors: { files: ['At least one .eml file is required.'] } },
            400,
          ),
        )
      }
      if (url.endsWith('/candidates')) {
        return Promise.resolve(jsonResponse([]))
      }
      return Promise.resolve(jsonResponse(vacancyDetails()))
    })
    vi.stubGlobal('fetch', fetchMock)

    const { wrapper } = mountView()
    await flushPromises()

    await openImportDialog(wrapper)
    await dropFiles([new File(['alice source'], 'alice.eml', { type: 'message/rfc822' })])
    await flushPromises()

    // The dialog stays open with the failure visible next to the drop zone.
    expect(document.body.textContent).toContain('At least one .eml file is required.')
    expect(document.body.querySelector('.dropzone')).not.toBeNull()
    expect(toastAdd).not.toHaveBeenCalled()
    wrapper.unmount()
  })

  it('US-14: HR sees the whole pipeline at a glance — name, notes, and status per candidate', async () => {
    const fetchMock = vi.fn((input: RequestInfo | URL) => {
      const url = String(input)
      if (url.endsWith('/candidates')) {
        return Promise.resolve(
          jsonResponse([
            candidateSummary(1),
            bobSummary(),
            candidateSummary(3, {
              sourceSenderName: null,
              sourceSenderEmail: null,
              sourceSubject: 'CV submission via web form',
            }),
          ]),
        )
      }
      return Promise.resolve(
        jsonResponse(
          vacancyDetails({
            requirements: [
              { id: 11, phrase: 'MIG welding', position: 0 },
              { id: 12, phrase: 'TIG welding', position: 1 },
            ],
            progress: { processedCandidates: 1, totalCandidates: 3 },
          }),
        ),
      )
    })
    vi.stubGlobal('fetch', fetchMock)

    const { wrapper } = mountView()
    await flushPromises()

    // Header: vacancy title with date and the send-all action parked for email templates.
    expect(wrapper.text()).toContain('Welder')
    expect(wrapper.text()).toContain('Opened')
    const sendAll = wrapper
      .findAll('button')
      .find((button) => button.text().includes('Send email to all candidates'))
    expect(sendAll).toBeDefined()
    expect((sendAll!.element as HTMLButtonElement).disabled).toBe(true)

    // Column order per the S3 sketch: Candidate | Received | CV | Notes | Review status | Actions.
    const headerCells = wrapper.findAll('.ctable__head th')
    expect(headerCells.map((cell) => cell.text())).toEqual([
      'Candidate',
      'Received',
      'CV',
      'Notes',
      'Review status',
      'Actions',
    ])
    expect(wrapper.find('.ctable__head').text()).not.toContain('Match Status')

    // Display name falls back to the sender, then to the email subject.
    expect(wrapper.text()).toContain('Alice Applicant')
    expect(wrapper.text()).toContain('Bob Builder')
    expect(wrapper.text()).toContain('CV submission via web form')
    expect(wrapper.find('[aria-label="Skills requirements"]').text()).toContain('MIG welding')
    expect(wrapper.find('[aria-label="Skills requirements"]').text()).toContain('TIG welding')
    expect(wrapper.text()).toContain('New')
    expect(wrapper.text()).toContain('Shortlisted')
    expect(wrapper.text()).toContain('Strong MIG experience')
    expect(wrapper.text()).not.toContain('Extraction pending')

    // Send is a placeholder until templates exist; Delete is available.
    const sendButtons = wrapper.findAll('button[aria-label*="Send email"]')
    expect(sendButtons).toHaveLength(3)
    expect(
      sendButtons.every((button) => (button.element as HTMLButtonElement).disabled),
    ).toBe(true)
    expect(wrapper.findAll('button[aria-label="Delete candidate"]')).toHaveLength(3)

    // The drop zone only lives inside the import dialog, which starts closed.
    expect(wrapper.find('.dropzone').exists()).toBe(false)
    expect(document.body.querySelector('.dropzone')).toBeNull()
    wrapper.unmount()
  })

  it('US-14: HR deletes a candidate after confirming', async () => {
    let deleted = false
    const fetchMock = vi.fn((input: RequestInfo | URL, init?: RequestInit) => {
      const url = String(input)
      if (init?.method === 'DELETE' && url.endsWith('/candidates/2')) {
        deleted = true
        return Promise.resolve(new Response(null, { status: 204 }))
      }
      if (url.endsWith('/candidates')) {
        return Promise.resolve(
          jsonResponse(deleted ? [candidateSummary(1)] : [candidateSummary(1), bobSummary()]),
        )
      }
      return Promise.resolve(jsonResponse(vacancyDetails()))
    })
    vi.stubGlobal('fetch', fetchMock)

    const { wrapper } = mountView()
    await flushPromises()

    const bobRow = wrapper.findAll('.crow').find((row) => row.text().includes('Bob Builder'))
    expect(bobRow).toBeDefined()
    await bobRow!.find('button[aria-label="Delete candidate"]').trigger('click')
    await flushPromises()

    // The confirm dialog names the candidate being deleted.
    expect(document.body.textContent).toContain('Bob Builder')
    expect(document.body.textContent).toContain("can't be undone")

    dialogButton('Delete candidate')?.click()
    await flushPromises()

    expect(deleted).toBe(true)
    expect(toastAdd).toHaveBeenCalledWith(
      expect.objectContaining({
        title: 'Candidate "Bob Builder" deleted successfully',
        color: 'success',
      }),
    )
    expect(wrapper.text()).toContain('Alice Applicant')
    expect(wrapper.text()).not.toContain('Bob Builder')
    wrapper.unmount()
  })

  it('US-14: HR is told when deleting a candidate fails and the list stays intact', async () => {
    const fetchMock = vi.fn((input: RequestInfo | URL, init?: RequestInit) => {
      const url = String(input)
      if (init?.method === 'DELETE' && url.endsWith('/candidates/2')) {
        return Promise.resolve(jsonResponse({ title: 'Server error' }, 500))
      }
      if (url.endsWith('/candidates')) {
        return Promise.resolve(jsonResponse([candidateSummary(1), bobSummary()]))
      }
      return Promise.resolve(jsonResponse(vacancyDetails()))
    })
    vi.stubGlobal('fetch', fetchMock)

    const { wrapper } = mountView()
    await flushPromises()

    const bobRow = wrapper.findAll('.crow').find((row) => row.text().includes('Bob Builder'))
    await bobRow!.find('button[aria-label="Delete candidate"]').trigger('click')
    await flushPromises()

    dialogButton('Delete candidate')?.click()
    await flushPromises()

    // Dialog stays open with the error; the loaded list is untouched.
    expect(document.body.textContent).toContain('API request failed with status 500')
    expect(document.body.textContent).toContain("can't be undone")
    expect(wrapper.text()).toContain('Bob Builder')
    expect(toastAdd).not.toHaveBeenCalled()
    wrapper.unmount()
  })

  it('US-12/US-14: a successful import replaces the empty state with the candidate list', async () => {
    let imported = false
    const fetchMock = vi.fn((input: RequestInfo | URL, init?: RequestInit) => {
      const url = String(input)
      if (init?.method === 'POST' && url.endsWith('/candidates/import')) {
        imported = true
        return Promise.resolve(
          jsonResponse({
            results: [
              {
                fileName: 'alice.eml',
                status: 'imported',
                error: null,
                candidate: importedCandidate(1, 'alice.eml'),
              },
            ],
          }),
        )
      }
      if (url.endsWith('/candidates')) {
        return Promise.resolve(jsonResponse(imported ? [candidateSummary(1)] : []))
      }
      return Promise.resolve(jsonResponse(vacancyDetails()))
    })
    vi.stubGlobal('fetch', fetchMock)

    const { wrapper } = mountView()
    await flushPromises()
    expect(wrapper.text()).toContain('No candidates yet')
    expect(wrapper.find('.crow').exists()).toBe(false)

    await openImportDialog(wrapper)
    await dropFiles([new File(['alice source'], 'alice.eml', { type: 'message/rfc822' })])
    await flushPromises()

    expect(wrapper.text()).not.toContain('No candidates yet')
    expect(wrapper.text()).toContain('Alice Applicant')
    wrapper.unmount()
  })

  it('domain: closed vacancy is read-only — no import is offered', async () => {
    vi.stubGlobal(
      'fetch',
      vi.fn((input: RequestInfo | URL) => {
        const url = String(input)
        if (url.endsWith('/candidates')) {
          return Promise.resolve(jsonResponse([]))
        }
        return Promise.resolve(
          jsonResponse(
            vacancyDetails({ status: 'closed', closedAt: '2026-08-29T00:00:00Z' }),
          ),
        )
      }),
    )

    const { wrapper } = mountView()
    await flushPromises()

    expect(wrapper.text()).toContain('Closed')
    expect(wrapper.text()).toContain('read-only')
    expect(
      wrapper.findAll('button').filter((button) => button.text().includes('Import')),
    ).toHaveLength(0)
    expect(wrapper.find('input[type="file"]').exists()).toBe(false)
    expect(document.body.querySelector('.dropzone')).toBeNull()
    wrapper.unmount()
  })

  it('domain: closed vacancy is read-only — candidate rows have no actions', async () => {
    vi.stubGlobal(
      'fetch',
      vi.fn((input: RequestInfo | URL) => {
        const url = String(input)
        if (url.endsWith('/candidates')) {
          return Promise.resolve(jsonResponse([candidateSummary(1)]))
        }
        return Promise.resolve(
          jsonResponse(
            vacancyDetails({
              status: 'closed',
              closedAt: '2026-08-29T00:00:00Z',
              progress: { processedCandidates: 0, totalCandidates: 1 },
            }),
          ),
        )
      }),
    )

    const { wrapper } = mountView()
    await flushPromises()

    expect(wrapper.text()).toContain('Alice Applicant')
    expect(wrapper.findAll('button[aria-label="Delete candidate"]')).toHaveLength(0)
    expect(wrapper.findAll('button[aria-label*="Send email"]')).toHaveLength(0)
    wrapper.unmount()
  })

  it('US-14: HR filters by review-status chips with live counts, combined AND with search', async () => {
    const fetchMock = vi.fn((input: RequestInfo | URL) => {
      const url = String(input)
      if (url.endsWith('/candidates')) {
        return Promise.resolve(
          jsonResponse([
            candidateSummary(1),
            bobSummary(),
            candidateSummary(3, {
              sourceSenderName: 'Carol Welder',
              sourceSenderEmail: 'carol@example.com',
              reviewStatus: 'shortlisted',
            }),
          ]),
        )
      }
      return Promise.resolve(jsonResponse(vacancyDetails()))
    })
    vi.stubGlobal('fetch', fetchMock)

    const { wrapper } = mountView()
    await flushPromises()

    const chips = () => wrapper.findAll('[aria-label="Filter by review status"] button')

    // Live counts per chip; the full list shows before any filter is active.
    expect(chips().map((chip) => chip.text().replace(/\s+/g, ' ').trim())).toEqual([
      'All 3',
      'New 1',
      'Flagged 0',
      'Shortlisted 2',
      'Rejected 0',
    ])
    expect(wrapper.findAll('.crow')).toHaveLength(3)

    // Only the active chip is highlighted.
    await chips()[3]!.trigger('click')
    expect(wrapper.findAll('.crow')).toHaveLength(2)
    expect(wrapper.text()).not.toContain('Alice Applicant')
    expect(chips()[3]!.attributes('aria-pressed')).toBe('true')
    expect(chips()[0]!.attributes('aria-pressed')).toBe('false')

    // Search narrows the filtered list further (AND semantics).
    await wrapper.find('input[aria-label="Search candidates"]').setValue('bob')
    const rows = wrapper.findAll('.crow')
    expect(rows).toHaveLength(1)
    expect(rows[0]!.text()).toContain('Bob Builder')

    // No match under the active filters shows the shared filter-empty state.
    await wrapper.find('input[aria-label="Search candidates"]').setValue('alice')
    expect(wrapper.findAll('.crow')).toHaveLength(0)
    expect(wrapper.text()).toContain('No candidates match')
    expect(wrapper.text()).not.toContain('No candidates yet')

    // Clear filters restores the full list and resets the search input.
    const clearButton = wrapper
      .findAll('button')
      .find((button) => button.text().includes('Clear filters'))
    await clearButton!.trigger('click')
    expect(wrapper.findAll('.crow')).toHaveLength(3)
    expect(
      (wrapper.find('input[aria-label="Search candidates"]').element as HTMLInputElement).value,
    ).toBe('')
    expect(chips()[0]!.attributes('aria-pressed')).toBe('true')
    wrapper.unmount()
  })

  it('US-14: HR searches candidates by sender email and email subject', async () => {
    const fetchMock = vi.fn((input: RequestInfo | URL) => {
      const url = String(input)
      if (url.endsWith('/candidates')) {
        return Promise.resolve(
          jsonResponse([
            candidateSummary(1),
            bobSummary(),
            candidateSummary(3, {
              sourceSenderName: null,
              sourceSenderEmail: null,
              sourceSubject: 'CV submission via web form',
            }),
          ]),
        )
      }
      return Promise.resolve(jsonResponse(vacancyDetails()))
    })
    vi.stubGlobal('fetch', fetchMock)

    const { wrapper } = mountView()
    await flushPromises()

    const search = wrapper.find('input[aria-label="Search candidates"]')

    await search.setValue('bob@example.com')
    expect(wrapper.findAll('.crow')).toHaveLength(1)
    expect(wrapper.text()).toContain('Bob Builder')
    expect(wrapper.text()).not.toContain('Alice Applicant')

    await search.setValue('web form')
    expect(wrapper.findAll('.crow')).toHaveLength(1)
    expect(wrapper.text()).toContain('CV submission via web form')

    await search.setValue('')
    expect(wrapper.findAll('.crow')).toHaveLength(3)
    wrapper.unmount()
  })

  it('US-14: HR sorts by received date, oldest first by default, toggling to newest', async () => {
    const fetchMock = vi.fn((input: RequestInfo | URL) => {
      const url = String(input)
      if (url.endsWith('/candidates')) {
        return Promise.resolve(
          jsonResponse([
            candidateSummary(1), // 2026-08-28
            candidateSummary(2, {
              sourceSenderName: 'Bob Builder',
              sourceSentAt: '2026-08-30T15:30:00Z',
            }),
            candidateSummary(3, {
              sourceSenderName: 'Carol Welder',
              sourceSentAt: '2026-08-25T08:00:00Z',
            }),
            candidateSummary(4, {
              sourceSenderName: 'Dave NoDate',
              sourceSentAt: null,
            }),
          ]),
        )
      }
      return Promise.resolve(jsonResponse(vacancyDetails()))
    })
    vi.stubGlobal('fetch', fetchMock)

    const { wrapper } = mountView()
    await flushPromises()

    const names = () => wrapper.findAll('.crow__name-text').map((cell) => cell.text())
    const sortHeader = () => wrapper.find('th[aria-sort]')

    // Oldest first by default; a missing sent-at sinks to the bottom.
    expect(sortHeader().attributes('aria-sort')).toBe('ascending')
    expect(names()).toEqual(['Carol Welder', 'Alice Applicant', 'Bob Builder', 'Dave NoDate'])

    await sortHeader().find('button').trigger('click')
    expect(sortHeader().attributes('aria-sort')).toBe('descending')
    expect(names()).toEqual(['Bob Builder', 'Alice Applicant', 'Carol Welder', 'Dave NoDate'])

    await sortHeader().find('button').trigger('click')
    expect(sortHeader().attributes('aria-sort')).toBe('ascending')
    expect(names()).toEqual(['Carol Welder', 'Alice Applicant', 'Bob Builder', 'Dave NoDate'])
    wrapper.unmount()
  })

  it('US-14: the CV column shows a paperclip when documents exist and a No CV badge otherwise', async () => {
    const fetchMock = vi.fn((input: RequestInfo | URL) => {
      const url = String(input)
      if (url.endsWith('/candidates')) {
        return Promise.resolve(
          jsonResponse([
            candidateSummary(1),
            candidateSummary(2, { sourceSenderName: 'Bob Builder', cvDocumentCount: 2 }),
            candidateSummary(3, { sourceSenderName: 'Carol NoCv', cvDocumentCount: 0 }),
          ]),
        )
      }
      return Promise.resolve(jsonResponse(vacancyDetails()))
    })
    vi.stubGlobal('fetch', fetchMock)

    const { wrapper } = mountView()
    await flushPromises()

    const cvCells = wrapper.findAll('.crow__cv')
    expect(cvCells).toHaveLength(3)
    expect(cvCells[0]!.text()).toContain('1 CV document')
    expect(cvCells[1]!.text()).toContain('2 CV documents')
    expect(cvCells[2]!.text()).toContain('No CV')
    expect(cvCells[2]!.text()).not.toContain('CV document')
    wrapper.unmount()
  })

  it('US-14: HR opens the review workspace by clicking a candidate row', async () => {
    const fetchMock = vi.fn((input: RequestInfo | URL) => {
      const url = String(input)
      if (url.endsWith('/candidates')) {
        return Promise.resolve(jsonResponse([candidateSummary(1), bobSummary()]))
      }
      return Promise.resolve(jsonResponse(vacancyDetails()))
    })
    vi.stubGlobal('fetch', fetchMock)

    const { wrapper, router } = mountView()
    await flushPromises()

    const bobRow = wrapper.findAll('.crow').find((row) => row.text().includes('Bob Builder'))
    await bobRow!.trigger('click')
    await flushPromises()

    expect(router.currentRoute.value.name).toBe('candidate-review')
    expect(router.currentRoute.value.fullPath).toBe('/vacancies/1/review/2')
    wrapper.unmount()
  })

  it('US-14: row action buttons do not navigate away from the list', async () => {
    const fetchMock = vi.fn((input: RequestInfo | URL) => {
      const url = String(input)
      if (url.endsWith('/candidates')) {
        return Promise.resolve(jsonResponse([candidateSummary(1), bobSummary()]))
      }
      return Promise.resolve(jsonResponse(vacancyDetails()))
    })
    vi.stubGlobal('fetch', fetchMock)

    const { wrapper, router } = mountView()
    await flushPromises()

    const bobRow = wrapper.findAll('.crow').find((row) => row.text().includes('Bob Builder'))
    await bobRow!.find('button[aria-label="Delete candidate"]').trigger('click')
    await flushPromises()

    // The delete dialog opened instead of a navigation.
    expect(document.body.textContent).toContain("can't be undone")
    expect(router.currentRoute.value.fullPath).toBe('/')
    wrapper.unmount()
  })
})
