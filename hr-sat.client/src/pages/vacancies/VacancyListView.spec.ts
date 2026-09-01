import { flushPromises, mount } from '@vue/test-utils'
import { afterEach, describe, expect, it, vi } from 'vitest'

import VacancyListView from './VacancyListView.vue'

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

function vacancy(overrides: Partial<Record<string, unknown>> = {}) {
  return {
    id: '1',
    title: 'Welder',
    openedOn: '2026-08-27',
    status: 'open',
    progress: { processedCandidates: 0, totalCandidates: 0 },
    ...overrides,
  }
}

function vacancyDetails(overrides: Partial<Record<string, unknown>> = {}) {
  return {
    ...vacancy(),
    closedAt: null,
    createdAt: '2026-08-27T00:00:00Z',
    requirements: [
      { id: 12, phrase: 'Blueprint reading', position: 1 },
      { id: 11, phrase: 'MIG welding', position: 0 },
    ],
    ...overrides,
  }
}

function dialogButton(text: string): HTMLButtonElement | undefined {
  return Array.from(document.body.querySelectorAll('button')).find((b) =>
    b.textContent?.includes(text),
  )
}

afterEach(() => {
  vi.clearAllMocks()
  vi.unstubAllGlobals()
  document.body.innerHTML = ''
})

describe('VacancyListView', () => {
  it('US-10: HR sees all vacancies with their status and progress', async () => {
    vi.stubGlobal(
      'fetch',
      vi.fn().mockResolvedValue(
        jsonResponse([
          vacancy({ progress: { processedCandidates: 3, totalCandidates: 30 } }),
          vacancy({
            id: '2',
            title: 'Forklift Operator',
            status: 'closed',
            progress: { processedCandidates: 30, totalCandidates: 30 },
          }),
        ]),
      ),
    )

    const wrapper = mount(VacancyListView)
    await flushPromises()

    expect(wrapper.text()).toContain('Welder')
    expect(wrapper.text()).toContain('Open')
    expect(wrapper.text()).toContain('3/30')
    expect(wrapper.text()).toContain('Forklift Operator')
    expect(wrapper.text()).toContain('Closed')
    expect(wrapper.text()).toContain('30/30')
  })

  it('domain: closed vacancy is read-only — row actions are hidden', async () => {
    vi.stubGlobal(
      'fetch',
      vi.fn().mockResolvedValue(jsonResponse([vacancy({ status: 'closed' })])),
    )

    const wrapper = mount(VacancyListView)
    await flushPromises()

    expect(wrapper.find('button[aria-label="Edit vacancy"]').exists()).toBe(false)
    expect(wrapper.find('button[aria-label="Delete vacancy"]').exists()).toBe(false)
  })

  it('US-9: HR creates a vacancy and sees it listed', async () => {
    let created = false
    const fetchMock = vi.fn((input: RequestInfo | URL, init?: RequestInit) => {
      if (init?.method === 'POST') {
        created = true
        return Promise.resolve(jsonResponse(vacancyDetails({ title: 'Senior Welder' })))
      }
      return Promise.resolve(
        jsonResponse(created ? [vacancy({ title: 'Senior Welder' })] : []),
      )
    })
    vi.stubGlobal('fetch', fetchMock)

    const wrapper = mount(VacancyListView)
    await flushPromises()

    const addButton = wrapper
      .findAll('button')
      .find((button) => button.text().includes('Add your first vacancy'))
    expect(addButton).toBeDefined()
    await addButton!.trigger('click')
    await flushPromises()

    const titleInput = document.body.querySelector<HTMLInputElement>(
      'input[placeholder="e.g. Senior Welder"]',
    )
    const requirementInput = document.body.querySelector<HTMLInputElement>(
      'input[aria-label="Requirement 1"]',
    )
    expect(titleInput).not.toBeNull()
    expect(requirementInput).not.toBeNull()
    titleInput!.value = 'Senior Welder'
    titleInput!.dispatchEvent(new Event('input', { bubbles: true }))
    requirementInput!.value = 'MIG welding'
    requirementInput!.dispatchEvent(new Event('input', { bubbles: true }))

    dialogButton('Create vacancy')?.click()
    await flushPromises()

    expect(toastAdd).toHaveBeenCalledWith(
      expect.objectContaining({ title: 'Vacancy created successfully', color: 'success' }),
    )
    expect(wrapper.text()).toContain('Senior Welder')
    wrapper.unmount()
  })

  it('US-11: HR is told when deleting a vacancy fails and the list stays intact', async () => {
    const fetchMock = vi.fn((input: RequestInfo | URL, init?: RequestInit) => {
      if (init?.method === 'DELETE') {
        return Promise.resolve(jsonResponse({ title: 'Server error' }, 500))
      }
      return Promise.resolve(jsonResponse([vacancy()]))
    })
    vi.stubGlobal('fetch', fetchMock)

    const wrapper = mount(VacancyListView)
    await flushPromises()

    await wrapper.find('button[aria-label="Delete vacancy"]').trigger('click')
    await flushPromises()

    dialogButton('Delete vacancy')?.click()
    await flushPromises()

    // Dialog stays open with the error; the loaded list is untouched.
    expect(document.body.textContent).toContain('API request failed with status 500')
    expect(document.body.textContent).toContain("can't be undone")
    expect(wrapper.text()).toContain('Welder')
    expect(wrapper.text()).not.toContain("Couldn't load vacancies")

    wrapper.unmount()
  })

  it('US-11: HR edits a vacancy and the form is prefilled with its details', async () => {
    const fetchMock = vi.fn((input: RequestInfo | URL) => {
      const url = String(input)
      if (url.endsWith('/api/vacancies/1')) {
        return Promise.resolve(jsonResponse(vacancyDetails()))
      }
      return Promise.resolve(jsonResponse([vacancy()]))
    })
    vi.stubGlobal('fetch', fetchMock)

    const wrapper = mount(VacancyListView)
    await flushPromises()

    await wrapper.find('button[aria-label="Edit vacancy"]').trigger('click')
    await flushPromises()

    expect(fetchMock).toHaveBeenCalledWith(
      expect.stringContaining('/api/vacancies/1'),
      expect.anything(),
    )

    const titleInput =
      document.body.querySelector<HTMLInputElement>('input[placeholder="e.g. Senior Welder"]')
    expect(titleInput?.value).toBe('Welder')

    // Requirements are prefilled in position order.
    const requirementValues = Array.from(
      document.body.querySelectorAll<HTMLInputElement>('input[aria-label^="Requirement "]'),
    ).map((input) => input.value)
    expect(requirementValues).toEqual(['MIG welding', 'Blueprint reading'])

    wrapper.unmount()
  })
})
