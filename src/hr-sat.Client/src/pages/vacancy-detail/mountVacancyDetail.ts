import { DOMWrapper, flushPromises, mount, type VueWrapper } from '@vue/test-utils'
import { afterEach, expect, vi } from 'vitest'
import { createMemoryHistory, createRouter } from 'vue-router'

import VacancyDetailView from './VacancyDetailView.vue'

// The toast module mock is per-file: `vi.mock` is hoisted, so each spec declares
// the mock itself and points its factory at this shared fn.
export const toastAdd = vi.fn()

export function jsonResponse(body: unknown, status = 200): Response {
  return new Response(JSON.stringify(body), {
    status,
    headers: { 'Content-Type': 'application/json' },
  })
}

export type FetchHandler = (url: string, init?: RequestInit) => Promise<Response> | undefined

export function stubFetch(vacancy: () => unknown, handler: FetchHandler): void {
  vi.stubGlobal(
    'fetch',
    vi.fn((input: RequestInfo | URL, init?: RequestInit) => {
      const url = String(input)
      const matched = handler(url, init)
      if (matched !== undefined) {
        return matched
      }
      if (url.endsWith('/vacancies/1')) {
        return Promise.resolve(jsonResponse(vacancy()))
      }
      // No Form Layout by default: the vacancy starts unmapped (404 is the
      // client's empty state, not a failure).
      if (url.endsWith('/form-layout')) {
        return Promise.resolve(
          jsonResponse({ title: 'FormLayouts.NotFound', detail: 'No form layout.' }, 404),
        )
      }
      throw new Error(`Unstubbed fetch: ${init?.method ?? 'GET'} ${url}`)
    }),
  )
}

export function openRound(overrides: Partial<Record<string, unknown>> = {}) {
  return {
    id: 1,
    roundNumber: 1,
    name: null,
    status: 'open',
    closedAt: null,
    candidateCount: 0,
    ...overrides,
  }
}

export function closedRound(overrides: Partial<Record<string, unknown>> = {}) {
  return openRound({ status: 'closed', closedAt: '2026-09-01T00:00:00Z', ...overrides })
}

export function vacancyDetails(overrides: Partial<Record<string, unknown>> = {}) {
  return {
    id: 1,
    title: 'Welder',
    openedOn: '2026-08-27',
    status: 'open',
    closedAt: null,
    createdAt: '2026-08-27T00:00:00Z',
    requirements: [{ id: 11, phrase: 'MIG welding', position: 0 }],
    rounds: [openRound()],
    progress: { processedCandidates: 0, totalCandidates: 0 },
    ...overrides,
  }
}

export function importedCandidate(id: number, filename: string) {
  return {
    id,
    reviewStatus: 'new',
    hireOutcome: 'none',
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
        downloadUrl: `/api/vacancies/1/rounds/1/candidates/${id}/cv-documents/${id * 10}`,
      },
    ],
  }
}

export function candidateSummary(id: number, overrides: Partial<Record<string, unknown>> = {}) {
  return {
    id,
    fullName: null,
    contactEmail: null,
    notes: null,
    reviewStatus: 'new',
    hireOutcome: 'none',
    isResubmitted: false,
    sourceSenderName: 'Alice Applicant',
    sourceSenderEmail: 'alice@example.com',
    sourceSubject: 'Application for Welder',
    sourceSentAt: '2026-08-28T09:00:00Z',
    cvDocumentCount: 1,
    ...overrides,
  }
}

export function bobSummary(overrides: Partial<Record<string, unknown>> = {}) {
  return candidateSummary(2, {
    sourceSenderName: 'Bob Builder',
    sourceSenderEmail: 'bob@example.com',
    sourceSubject: 'Bob application',
    reviewStatus: 'shortlisted',
    notes: 'Strong MIG experience',
    ...overrides,
  })
}

// The Form Layout GET wire shape spells roles lowercased; writes take camelCase.
export function formLayoutDto(overrides: Partial<Record<string, unknown>> = {}) {
  return {
    id: 5,
    vacancyId: 1,
    headerSnapshot: ['Timestamp', 'Nama Lengkap', 'Email aktif', 'Pengalaman kerja'],
    columns: [
      { ordinal: 1, role: 'name', label: 'Candidate name' },
      { ordinal: 2, role: 'contactemail', label: null },
      { ordinal: 3, role: null, label: null },
    ],
    isValid: true,
    candidatesUpdated: 0,
    typedOverridesKept: 0,
    ...overrides,
  }
}

export function mountView(id = '1') {
  const router = createRouter({
    history: createMemoryHistory(),
    routes: [
      { path: '/', name: 'vacancy-list', component: { template: '<div />' } },
      { path: '/vacancies/:id', name: 'vacancy-detail', component: { template: '<div />' } },
      {
        path: '/vacancies/:id/rounds/:roundId/review/:candidateId',
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

export async function mountViewWithQuery(id: string, query: Record<string, string>) {
  const router = createRouter({
    history: createMemoryHistory(),
    routes: [
      { path: '/', name: 'vacancy-list', component: { template: '<div />' } },
      { path: '/vacancies/:id', name: 'vacancy-detail', component: { template: '<div />' } },
      {
        path: '/vacancies/:id/rounds/:roundId/review/:candidateId',
        name: 'candidate-review',
        component: { template: '<div />' },
      },
    ],
  })
  void router.push({ path: `/vacancies/${id}`, query })
  await router.isReady()
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

export function bodyElement(selector: string): Element {
  const element = document.body.querySelector(selector)
  if (!element) {
    throw new Error(`Expected "${selector}" to exist in document.body`)
  }
  return element
}

export function dialogButton(text: string): HTMLButtonElement | undefined {
  return Array.from(document.body.querySelectorAll('button')).find((button) =>
    button.textContent?.includes(text),
  )
}

export async function openImportDialog(wrapper: VueWrapper) {
  const button = wrapper
    .findAll('button')
    .find((candidate) => candidate.text().includes('Import candidates'))
  expect(button, 'an Import candidates button').toBeDefined()
  await button!.trigger('click')
  await flushPromises()
}

export function dropFiles(files: File[]) {
  return new DOMWrapper(bodyElement('.dropzone')).trigger('drop', { dataTransfer: { files } })
}

// Picks a column in one of the Form Layout panel's role selects (teleported).
export async function pickRoleColumn(roleLabel: string, optionText: string) {
  const trigger = Array.from(document.body.querySelectorAll('button')).find(
    (candidate) => candidate.getAttribute('aria-label') === roleLabel,
  )
  expect(trigger, `a "${roleLabel}" role select`).toBeDefined()
  await new DOMWrapper(trigger as HTMLElement).trigger('keydown', { key: 'ArrowDown' })
  await flushPromises()
  const option = Array.from(document.body.querySelectorAll<HTMLElement>('[role="option"]')).find(
    (candidate) => candidate.textContent?.includes(optionText),
  )
  expect(option, `an option containing "${optionText}"`).toBeDefined()
  await new DOMWrapper(option as HTMLElement).trigger('keydown', { key: 'Enter' })
  await flushPromises()
}

afterEach(() => {
  vi.clearAllMocks()
  vi.unstubAllGlobals()
  document.body.innerHTML = ''
  delete (window.navigator as { clipboard?: unknown }).clipboard
})
