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

  it('US-10: hiring progress and the filled state are separate from candidate progress', async () => {
    vi.stubGlobal(
      'fetch',
      vi.fn().mockResolvedValue(
        jsonResponse([
          vacancy({
            progress: { processedCandidates: 3, totalCandidates: 30 },
            hiring: { neededHires: 10, activeHires: 0 },
          }),
          vacancy({
            id: '2',
            title: 'Filled Operator',
            progress: { processedCandidates: 4, totalCandidates: 12 },
            hiring: { neededHires: 2, activeHires: 2 },
          }),
          vacancy({
            id: '3',
            title: 'No Hiring Target',
            progress: { processedCandidates: 1, totalCandidates: 4 },
          }),
        ]),
      ),
    )

    const wrapper = mount(VacancyListView)
    await flushPromises()

    expect(wrapper.text()).toContain('0/10 hired \u00b7 10 to go')
    expect(wrapper.text()).toContain('Filled')

    const v1Row = wrapper.findAll('tbody tr').find((row) => row.text().includes('No Hiring Target'))
    expect(v1Row).toBeDefined()
    expect(v1Row!.text()).toContain('1/4')
    expect(v1Row!.text()).not.toContain('hired')
    expect(v1Row!.text()).not.toContain('Filled')
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
    let sentBody: unknown
    const fetchMock = vi.fn((input: RequestInfo | URL, init?: RequestInit) => {
      if (init?.method === 'POST') {
        created = true
        sentBody = JSON.parse(String(init.body))
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
    const neededHiresInput = document.body.querySelector<HTMLInputElement>(
      'input[placeholder="e.g. 10"]',
    )
    expect(titleInput).not.toBeNull()
    expect(requirementInput).not.toBeNull()
    expect(neededHiresInput).not.toBeNull()
    titleInput!.value = 'Senior Welder'
    titleInput!.dispatchEvent(new Event('input', { bubbles: true }))
    requirementInput!.value = 'MIG welding'
    requirementInput!.dispatchEvent(new Event('input', { bubbles: true }))
    neededHiresInput!.value = '3'
    neededHiresInput!.dispatchEvent(new Event('input', { bubbles: true }))

    dialogButton('Create vacancy')?.click()
    await flushPromises()

    expect(sentBody).toEqual(
      expect.objectContaining({ neededHires: 3 }),
    )
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

  it('US-11: HR sees when an edited hiring target is saved', async () => {
    let neededHires = 5
    let sentBody: unknown
    const fetchMock = vi.fn((input: RequestInfo | URL, init?: RequestInit) => {
      const url = String(input)
      if (init?.method === 'PUT' && url.endsWith('/api/vacancies/1')) {
        sentBody = JSON.parse(String(init.body))
        neededHires = 7
        return Promise.resolve(
          jsonResponse(vacancyDetails({ hiring: { neededHires, activeHires: 0 } })),
        )
      }
      if (url.endsWith('/api/vacancies/1')) {
        return Promise.resolve(
          jsonResponse(vacancyDetails({ hiring: { neededHires, activeHires: 0 } })),
        )
      }
      return Promise.resolve(
        jsonResponse([vacancy({ hiring: { neededHires, activeHires: 0 } })]),
      )
    })
    vi.stubGlobal('fetch', fetchMock)

    const wrapper = mount(VacancyListView)
    await flushPromises()

    await wrapper.find('button[aria-label="Edit vacancy"]').trigger('click')
    await flushPromises()

    expect(document.body.textContent).toContain('Saved')
    const saveButton = dialogButton('Save changes')
    expect(saveButton?.disabled).toBe(true)

    const neededHiresInput = document.body.querySelector<HTMLInputElement>(
      'input[aria-label="Needed hires"]',
    )
    neededHiresInput!.value = '7'
    neededHiresInput!.dispatchEvent(new Event('input', { bubbles: true }))
    await flushPromises()

    expect(document.body.textContent).toContain('Unsaved changes')
    expect(saveButton?.disabled).toBe(false)

    saveButton?.click()
    await flushPromises()

    expect(sentBody).toEqual(expect.objectContaining({ neededHires: 7 }))
    expect(document.body.textContent).toContain('Saved')
    expect(document.body.textContent).toContain('7 people')
    expect(saveButton?.disabled).toBe(true)
    expect(document.body.textContent).toContain('Cancel')
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

  it('US-9: HR can clear the hiring target while editing an open vacancy', async () => {
    let sentBody: unknown
    const fetchMock = vi.fn((input: RequestInfo | URL, init?: RequestInit) => {
      const url = String(input)
      if (init?.method === 'PUT' && url.endsWith('/api/vacancies/1')) {
        sentBody = JSON.parse(String(init.body))
        return Promise.resolve(jsonResponse(vacancyDetails({ hiring: null })))
      }
      if (url.endsWith('/api/vacancies/1')) {
        return Promise.resolve(
          jsonResponse(vacancyDetails({ hiring: { neededHires: 5, activeHires: 0 } })),
        )
      }
      return Promise.resolve(
        jsonResponse([vacancy({ hiring: { neededHires: 5, activeHires: 0 } })]),
      )
    })
    vi.stubGlobal('fetch', fetchMock)

    const wrapper = mount(VacancyListView)
    await flushPromises()

    await wrapper.find('button[aria-label="Edit vacancy"]').trigger('click')
    await flushPromises()

    const neededHiresInput = document.body.querySelector<HTMLInputElement>(
      'input[placeholder="e.g. 10"]',
    )
    expect(neededHiresInput?.value).toBe('5')
    const clearNeededHiresButton = document.body.querySelector<HTMLButtonElement>(
      'button[aria-label="Clear needed hires"]',
    )
    expect(clearNeededHiresButton).not.toBeNull()
    clearNeededHiresButton!.click()
    await flushPromises()
    expect(neededHiresInput?.value).toBe('')

    dialogButton('Save changes')?.click()
    await flushPromises()

    expect(sentBody).toEqual(
      expect.objectContaining({ neededHires: null }),
    )
    wrapper.unmount()
  })
})
