import { DOMWrapper, flushPromises, mount, type VueWrapper } from '@vue/test-utils'
import { afterEach, expect, vi } from 'vitest'
import { createMemoryHistory, createRouter } from 'vue-router'

import SpecAppShell from './SpecAppShell.vue'

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
      // No Messaging summary by default: the send dialogs read an empty round.
      if (url.endsWith('/messaging-summary')) {
        return Promise.resolve(jsonResponse([]))
      }
      // No Screening Rule set by default (404 is the client's empty state, not
      // a failure). The preview endpoint ends in `/preview`, so this never
      // shadows it.
      if (url.endsWith('/screening-rules')) {
        return Promise.resolve(
          jsonResponse({ title: 'ScreeningRules.NotFound', detail: 'No screening rules.' }, 404),
        )
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
    intakeSource: 'email',
    screenedOut: false,
    firedRules: [] as { index: number; display: string }[],
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

/** The candidate fields the paged-list fixtures read; `candidateSummary` satisfies it. */
export interface CandidateListItem {
  id: number
  reviewStatus: string
  hireOutcome: string
  screenedOut: boolean
  fullName: string | null
  contactEmail: string | null
  sourceSenderName: string | null
  sourceSenderEmail: string | null
  sourceSubject: string | null
  sourceSentAt: string | null
}

// Counts mirror PostgresCandidateListReader.ReadCountsAsync: computed over the
// whole round, split by screened_out, never filter- nor toggle-aware.
function listCounts(items: CandidateListItem[]) {
  const nonScreened = items.filter((item) => !item.screenedOut)
  const shortlisted = nonScreened.filter((item) => item.reviewStatus === 'shortlisted')
  const byStatus = (status: string) =>
    nonScreened.filter((item) => item.reviewStatus === status).length
  const byOutcome = (outcome: string) =>
    shortlisted.filter((item) => item.hireOutcome === outcome).length
  return {
    status: {
      new: byStatus('new'),
      flagged: byStatus('flagged'),
      shortlisted: shortlisted.length,
      rejected: byStatus('rejected'),
    },
    outcome: {
      any: nonScreened.length,
      undecided: byOutcome('none'),
      hired: byOutcome('hired'),
      runaway: byOutcome('runaway'),
      declined: byOutcome('declined'),
    },
    screenedOut: items.length - nonScreened.length,
  }
}

/**
 * A static one-page envelope for specs that only render the list: items pass
 * through verbatim, counts derive from them (the fixture IS the whole round),
 * and the totals equal the non-screened length.
 */
export function pagedCandidates(
  items: CandidateListItem[],
  overrides: Record<string, unknown> = {},
) {
  const nonScreened = items.filter((item) => !item.screenedOut)
  return {
    items,
    page: 1,
    pageSize: 100,
    total: nonScreened.length,
    filteredTotal: nonScreened.length,
    counts: listCounts(items),
    ...overrides,
  }
}

/**
 * A mini server for filter-dependent specs: parses the list query out of the
 * request URL and mirrors PostgresCandidateListReader — counts over the whole
 * round (never filter- nor toggle-aware), `total` over the screened scope,
 * `filteredTotal` over scope + filters, rows sorted NULLs-last by received-at
 * with an id tiebreak, page size 100.
 */
export function pagedCandidatesFor(url: string, items: CandidateListItem[]) {
  const params = new URL(url, 'http://localhost').searchParams
  const status = params.get('status') ?? 'all'
  const outcome = params.get('outcome') ?? 'any'
  const query = (params.get('query') ?? '').trim().toLowerCase()
  const oldestFirst = params.get('sort') === 'oldest'
  const screenedAll = params.get('screened') === 'all'
  const parsedPage = Number.parseInt(params.get('page') ?? '1', 10)
  const page = Number.isFinite(parsedPage) && parsedPage > 1 ? parsedPage : 1
  const pageSize = 100

  const scoped = screenedAll ? items : items.filter((item) => !item.screenedOut)
  const filtered = scoped.filter((item) => {
    if (status !== 'all' && item.reviewStatus !== status) {
      return false
    }
    if (outcome !== 'any') {
      if (item.reviewStatus !== 'shortlisted') {
        return false
      }
      const matches =
        outcome === 'undecided' ? item.hireOutcome === 'none' : item.hireOutcome === outcome
      if (!matches) {
        return false
      }
    }
    if (query !== '') {
      const haystack = [
        item.fullName,
        item.contactEmail,
        item.sourceSenderName,
        item.sourceSenderEmail,
        item.sourceSubject,
      ].map((value) => (value ?? '').toLowerCase())
      if (!haystack.some((value) => value.includes(query))) {
        return false
      }
    }
    return true
  })

  const direction = oldestFirst ? 1 : -1
  const sorted = [...filtered].sort((left, right) => {
    if (left.sourceSentAt === null || right.sourceSentAt === null) {
      if (left.sourceSentAt === null && right.sourceSentAt === null) {
        return left.id - right.id
      }
      return left.sourceSentAt === null ? 1 : -1
    }
    const byDate = left.sourceSentAt.localeCompare(right.sourceSentAt) * direction
    return byDate !== 0 ? byDate : left.id - right.id
  })

  return {
    items: sorted.slice((page - 1) * pageSize, page * pageSize),
    page,
    pageSize,
    total: scoped.length,
    filteredTotal: filtered.length,
    counts: listCounts(items),
  }
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

const testRoutes = [
  { path: '/', name: 'vacancy-list', component: { template: '<div />' } },
  { path: '/vacancies/:id', name: 'vacancy-detail', component: { template: '<div />' } },
  {
    path: '/vacancies/:id/rounds/:roundId/review/:candidateId',
    name: 'candidate-review',
    component: { template: '<div />' },
  },
]

// The shell mirrors the application root: UApp provides the TooltipProvider
// that UTooltip (toolbar toggle, rule chips) injects.
function mountViewRoot(id: string, router: ReturnType<typeof createRouter>) {
  return mount(SpecAppShell, {
    props: { id },
    global: {
      plugins: [router],
      stubs: {
        RouterLink: { template: '<a><slot /></a>' },
      },
    },
  })
}

export function mountView(id = '1') {
  const router = createRouter({
    history: createMemoryHistory(),
    routes: testRoutes,
  })
  const wrapper = mountViewRoot(id, router)
  return { router, wrapper }
}

export async function mountViewWithQuery(id: string, query: Record<string, string>) {
  const router = createRouter({
    history: createMemoryHistory(),
    routes: testRoutes,
  })
  void router.push({ path: `/vacancies/${id}`, query })
  await router.isReady()
  const wrapper = mountViewRoot(id, router)
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
