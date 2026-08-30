import { flushPromises, mount, type DOMWrapper } from '@vue/test-utils'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { toast } from 'vue-sonner'

import VacancyDetailView from './VacancyDetailView.vue'

vi.mock('vue-sonner', () => ({
  toast: {
    error: vi.fn(),
    success: vi.fn(),
  },
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

function dropFiles(dropzone: DOMWrapper<Element>, files: File[]) {
  return dropzone.trigger('drop', { dataTransfer: { files } })
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

    await dropFiles(wrapper.find('.dropzone'), [
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
    expect(toast.success).toHaveBeenCalledWith(
      'Import complete: 1 imported, 1 failed',
      expect.anything(),
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

    await dropFiles(wrapper.find('.dropzone'), [
      new File(['alice source'], 'alice.eml', { type: 'message/rfc822' }),
    ])
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
      return Promise.resolve(jsonResponse(vacancyDetails()))
    })
    vi.stubGlobal('fetch', fetchMock)

    const wrapper = mountView()
    await flushPromises()

    await dropFiles(wrapper.find('.dropzone'), [
      new File(['alice source'], 'alice.eml', { type: 'message/rfc822' }),
    ])
    await flushPromises()

    expect(wrapper.text()).toContain('At least one .eml file is required.')
    expect(toast.success).not.toHaveBeenCalled()
    wrapper.unmount()
  })

  it('domain: closed vacancy is read-only — no import is offered', async () => {
    vi.stubGlobal(
      'fetch',
      vi.fn().mockResolvedValue(
        jsonResponse(
          vacancyDetails({ status: 'closed', closedAt: '2026-08-29T00:00:00Z' }),
        ),
      ),
    )

    const wrapper = mountView()
    await flushPromises()

    expect(wrapper.text()).toContain('Closed')
    expect(wrapper.text()).toContain('read-only')
    expect(wrapper.find('input[type="file"]').exists()).toBe(false)
    expect(wrapper.find('.dropzone').exists()).toBe(false)
    wrapper.unmount()
  })
})
