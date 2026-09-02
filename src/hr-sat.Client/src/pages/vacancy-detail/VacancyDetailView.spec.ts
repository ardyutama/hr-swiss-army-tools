import { DOMWrapper, flushPromises, mount, type VueWrapper } from '@vue/test-utils'
import { afterEach, describe, expect, it, vi } from 'vitest'

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
    extractionStatus: 'pending',
    match: { matchedRequirements: 2, totalRequirements: 3 },
    sourceSenderName: 'Alice Applicant',
    sourceSenderEmail: 'alice@example.com',
    sourceSubject: 'Application for Welder',
    sourceSentAt: '2026-08-28T09:00:00Z',
    ...overrides,
  }
}

function bobSummary() {
  return candidateSummary(2, {
    sourceSenderName: 'Bob Builder',
    sourceSenderEmail: 'bob@example.com',
    sourceSubject: 'Bob application',
    reviewStatus: 'shortlisted',
    extractionStatus: 'failed',
    notes: 'Strong MIG experience',
  })
}

function mountView(id = '1') {
  return mount(VacancyDetailView, {
    props: { id },
    global: {
      stubs: {
        RouterLink: { template: '<a><slot /></a>' },
      },
    },
  })
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

    const wrapper = mountView()
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

    const wrapper = mountView()
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

    const wrapper = mountView()
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

  it('US-14: HR sees the whole pipeline at a glance — name, match status, notes, and status per candidate', async () => {
    const fetchMock = vi.fn((input: RequestInfo | URL) => {
      const url = String(input)
      if (url.endsWith('/candidates')) {
        return Promise.resolve(jsonResponse([candidateSummary(1), bobSummary()]))
      }
      return Promise.resolve(
        jsonResponse(
          vacancyDetails({
            progress: { processedCandidates: 1, totalCandidates: 2 },
          }),
        ),
      )
    })
    vi.stubGlobal('fetch', fetchMock)

    const wrapper = mountView()
    await flushPromises()

    // Header: vacancy title with date and the send-all action parked for email templates.
    expect(wrapper.text()).toContain('Welder')
    expect(wrapper.text()).toContain('Opened')
    const sendAll = wrapper
      .findAll('button')
      .find((button) => button.text().includes('Send email to all candidates'))
    expect(sendAll).toBeDefined()
    expect((sendAll!.element as HTMLButtonElement).disabled).toBe(true)

    // One row per candidate under the sketch's columns.
    const header = wrapper.find('.ctable__head')
    expect(header.text()).toContain('Candidate')
    expect(header.text()).toContain('Match')
    expect(header.text()).toContain('Notes')
    expect(header.text()).toContain('Status')

    expect(wrapper.text()).toContain('Alice Applicant')
    expect(wrapper.text()).toContain('Bob Builder')
    expect(wrapper.text()).toContain('2 / 3')
    expect(wrapper.text()).toContain('Extraction pending')
    expect(wrapper.text()).toContain('Extraction failed')
    expect(wrapper.text()).toContain('New')
    expect(wrapper.text()).toContain('Shortlisted')
    expect(wrapper.text()).toContain('Strong MIG experience')

    // Send is a placeholder until templates exist; Delete is available.
    const sendButtons = wrapper.findAll('button[aria-label*="Send email"]')
    expect(sendButtons).toHaveLength(2)
    expect(
      sendButtons.every((button) => (button.element as HTMLButtonElement).disabled),
    ).toBe(true)
    expect(wrapper.findAll('button[aria-label="Delete candidate"]')).toHaveLength(2)

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

    const wrapper = mountView()
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

    const wrapper = mountView()
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

    const wrapper = mountView()
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

    const wrapper = mountView()
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

    const wrapper = mountView()
    await flushPromises()

    expect(wrapper.text()).toContain('Alice Applicant')
    expect(wrapper.findAll('button[aria-label="Delete candidate"]')).toHaveLength(0)
    expect(wrapper.findAll('button[aria-label*="Send email"]')).toHaveLength(0)
    wrapper.unmount()
  })
})
