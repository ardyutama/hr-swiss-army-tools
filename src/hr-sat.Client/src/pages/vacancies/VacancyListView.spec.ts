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
    reviewCounts: { new: 0, flagged: 0, shortlisted: 0, rejected: 0 },
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

    const allChip = wrapper
      .find('[role="group"][aria-label="Filter by vacancy status"]')
      .findAll('button')
      .find((chip) => chip.text().includes('All'))
    await allChip!.trigger('click')

    expect(wrapper.text()).toContain('Welder')
    expect(wrapper.text()).toContain('Open')
    expect(wrapper.text()).toContain('3/30')
    expect(wrapper.text()).toContain('Forklift Operator')
    expect(wrapper.text()).toContain('Closed')
    expect(wrapper.text()).toContain('30/30')
  })

  it('US-10: the list defaults to open vacancies and the chips carry live counts', async () => {
    vi.stubGlobal(
      'fetch',
      vi.fn().mockResolvedValue(
        jsonResponse([
          vacancy({ title: 'Open Welder' }),
          vacancy({ id: '2', title: 'Closed Fitter', status: 'closed' }),
          vacancy({ id: '3', title: 'Closed Turner', status: 'closed' }),
        ]),
      ),
    )

    const wrapper = mount(VacancyListView)
    await flushPromises()

    expect(wrapper.text()).toContain('Open Welder')
    expect(wrapper.text()).not.toContain('Closed Fitter')
    expect(wrapper.text()).not.toContain('Closed Turner')

    const chips = wrapper
      .find('[role="group"][aria-label="Filter by vacancy status"]')
      .findAll('button')
    expect(chips.find((chip) => chip.text().includes('Open'))?.text()).toContain('1')
    expect(chips.find((chip) => chip.text().includes('Closed'))?.text()).toContain('2')
    expect(chips.find((chip) => chip.text().includes('All'))?.text()).toContain('3')

    await chips.find((chip) => chip.text().includes('Closed'))!.trigger('click')

    expect(wrapper.text()).not.toContain('Open Welder')
    expect(wrapper.text()).toContain('Closed Fitter')
    expect(wrapper.text()).toContain('Closed Turner')
  })

  it('US-10: HR narrows the vacancy list by searching titles', async () => {
    vi.stubGlobal(
      'fetch',
      vi.fn().mockResolvedValue(
        jsonResponse([vacancy({ title: 'MIG Welder' }), vacancy({ id: '2', title: 'Forklift Operator' })]),
      ),
    )

    const wrapper = mount(VacancyListView)
    await flushPromises()

    await wrapper.find('input[aria-label="Search vacancies"]').setValue('mig')

    expect(wrapper.text()).toContain('MIG Welder')
    expect(wrapper.text()).not.toContain('Forklift Operator')
  })

  it('US-10: HR clears the filters when no vacancies match', async () => {
    vi.stubGlobal(
      'fetch',
      vi.fn().mockResolvedValue(jsonResponse([vacancy({ status: 'closed' })])),
    )

    const wrapper = mount(VacancyListView)
    await flushPromises()

    // The default Open filter hides the only (closed) vacancy.
    expect(wrapper.text()).toContain('No vacancies match these filters')
    expect(wrapper.text()).not.toContain('Welder')

    const clearButton = wrapper
      .findAll('button')
      .find((button) => button.text().includes('Clear filters'))
    await clearButton!.trigger('click')

    expect(wrapper.text()).toContain('Welder')
  })

  it('domain: a vacancy-list load failure uses friendly retry copy', async () => {
    vi.stubGlobal('fetch', vi.fn().mockResolvedValue(jsonResponse({ title: 'Server error' }, 500)))

    const wrapper = mount(VacancyListView)
    await flushPromises()

    expect(wrapper.find('[role="alert"]').text()).toContain('Something went wrong')
    expect(wrapper.find('[role="alert"]').text()).toContain('Please try again.')
    wrapper.unmount()
  })

  it('US-10: hiring progress and the filled state sit in their own column', async () => {
    vi.stubGlobal(
      'fetch',
      vi.fn().mockResolvedValue(
        jsonResponse([
          vacancy({
            progress: { processedCandidates: 3, totalCandidates: 30 },
            reviewCounts: { new: 27, flagged: 0, shortlisted: 2, rejected: 1 },
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

    const noTargetRow = wrapper.findAll('tbody tr').find((row) => row.text().includes('No Hiring Target'))
    expect(noTargetRow).toBeDefined()
    expect(noTargetRow!.text()).toContain('1/4')
    expect(noTargetRow!.text()).toContain('—')
    expect(noTargetRow!.text()).not.toContain('hired')
    expect(noTargetRow!.text()).not.toContain('Filled')

    // Review-count line renders under the bar and omits zero-count statuses.
    const welderRow = wrapper.findAll('tbody tr').find((row) => row.text().includes('Welder'))
    const countsCell = welderRow!.find('.vrow__review-counts')
    expect(countsCell.exists()).toBe(true)
    expect(countsCell.text()).toBe('27 new · 2 shortlisted · 1 rejected')
    expect(countsCell.text()).not.toContain('flagged')
    // An all-zero pipeline renders no count line at all.
    const filledRow = wrapper.findAll('tbody tr').find((row) => row.text().includes('Filled Operator'))
    expect(filledRow!.find('.vrow__review-counts').exists()).toBe(false)
  })

  it('domain: closed vacancy is read-only — it can be purged but not edited (US-11)', async () => {
    vi.stubGlobal(
      'fetch',
      vi.fn().mockResolvedValue(jsonResponse([vacancy({ status: 'closed' })])),
    )

    const wrapper = mount(VacancyListView)
    await flushPromises()

    const closedChip = wrapper
      .find('[role="group"][aria-label="Filter by vacancy status"]')
      .findAll('button')
      .find((chip) => chip.text().includes('Closed'))
    await closedChip!.trigger('click')

    expect(wrapper.text()).toContain('Welder')
    expect(wrapper.find('button[aria-label="Edit vacancy"]').exists()).toBe(false)
    expect(wrapper.find('button[aria-label="Purge vacancy"]').exists()).toBe(true)
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

  it('US-11: HR is told when purging a vacancy fails and the list stays intact', async () => {
    const fetchMock = vi.fn((input: RequestInfo | URL, init?: RequestInit) => {
      if (init?.method === 'DELETE') {
        return Promise.resolve(jsonResponse({ title: 'Server error' }, 500))
      }
      return Promise.resolve(jsonResponse([vacancy()]))
    })
    vi.stubGlobal('fetch', fetchMock)

    const wrapper = mount(VacancyListView)
    await flushPromises()

    await wrapper.find('button[aria-label="Purge vacancy"]').trigger('click')
    await flushPromises()

    dialogButton('Purge vacancy')?.click()
    await flushPromises()

    // Dialog stays open with the error; the loaded list is untouched.
    expect(document.body.textContent).toContain('Something went wrong')
    expect(document.body.textContent).toContain('Please try again.')
    expect(document.body.textContent).toContain("can't be undone")
    expect(wrapper.text()).toContain('Welder')
    expect(wrapper.text()).not.toContain("Couldn't load vacancies")

    wrapper.unmount()
  })

  it('US-11: the purge dialog states it removes the vacancy together with its candidate information', async () => {
    vi.stubGlobal('fetch', vi.fn().mockResolvedValue(jsonResponse([vacancy()])))

    const wrapper = mount(VacancyListView)
    await flushPromises()

    await wrapper.find('button[aria-label="Purge vacancy"]').trigger('click')
    await flushPromises()

    expect(document.body.textContent).toContain('Purge vacancy?')
    expect(document.body.textContent).toContain('together with all candidate information it owns')
    expect(dialogButton('Purge vacancy')).toBeDefined()

    wrapper.unmount()
  })

  it('US-11: HR purges a vacancy and sees it gone from the list', async () => {
    let purged = false
    const fetchMock = vi.fn((input: RequestInfo | URL, init?: RequestInit) => {
      if (init?.method === 'DELETE') {
        purged = true
        return Promise.resolve(new Response(null, { status: 204 }))
      }
      return Promise.resolve(jsonResponse(purged ? [] : [vacancy()]))
    })
    vi.stubGlobal('fetch', fetchMock)

    const wrapper = mount(VacancyListView)
    await flushPromises()

    await wrapper.find('button[aria-label="Purge vacancy"]').trigger('click')
    await flushPromises()
    dialogButton('Purge vacancy')?.click()
    await flushPromises()

    expect(toastAdd).toHaveBeenCalledWith(
      expect.objectContaining({ title: 'Vacancy "Welder" purged successfully', color: 'success' }),
    )
    expect(wrapper.text()).toContain('No vacancies yet')

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

  it('US-10: HR sorts the vacancy list by title, opened date, and progress', async () => {
    vi.stubGlobal(
      'fetch',
      vi.fn().mockResolvedValue(
        jsonResponse([
          vacancy({ title: 'Charlie', openedOn: '2026-08-27', progress: { processedCandidates: 3, totalCandidates: 30 } }),
          vacancy({ id: '2', title: 'Alpha', openedOn: '2026-09-01', progress: { processedCandidates: 2, totalCandidates: 4 } }),
          vacancy({ id: '3', title: 'Bravo', openedOn: '2026-08-20', progress: { processedCandidates: 0, totalCandidates: 0 } }),
        ]),
      ),
    )

    const wrapper = mount(VacancyListView)
    await flushPromises()

    const rowTitles = () =>
      wrapper.findAll('tbody tr').map((row) => row.find('.vrow__title').text())
    const header = (text: string) =>
      wrapper.findAll('th').find((th) => th.text().includes(text))

    // Default: server order, no sort announced.
    expect(rowTitles()).toEqual(['Charlie', 'Alpha', 'Bravo'])
    expect(header('Vacancy')!.attributes('aria-sort')).toBe('none')

    // Title: asc → desc → back to default.
    await header('Vacancy')!.find('button').trigger('click')
    expect(rowTitles()).toEqual(['Alpha', 'Bravo', 'Charlie'])
    expect(header('Vacancy')!.attributes('aria-sort')).toBe('ascending')

    await header('Vacancy')!.find('button').trigger('click')
    expect(rowTitles()).toEqual(['Charlie', 'Bravo', 'Alpha'])
    expect(header('Vacancy')!.attributes('aria-sort')).toBe('descending')

    await header('Vacancy')!.find('button').trigger('click')
    expect(rowTitles()).toEqual(['Charlie', 'Alpha', 'Bravo'])
    expect(header('Vacancy')!.attributes('aria-sort')).toBe('none')

    // Opened date: business date order, not creation order.
    await header('Opened')!.find('button').trigger('click')
    expect(rowTitles()).toEqual(['Bravo', 'Charlie', 'Alpha'])
    expect(header('Opened')!.attributes('aria-sort')).toBe('ascending')
    expect(header('Vacancy')!.attributes('aria-sort')).toBe('none')

    // Progress: processed ratio; a candidate-less vacancy ranks last.
    await header('Progress')!.find('button').trigger('click')
    expect(rowTitles()).toEqual(['Charlie', 'Alpha', 'Bravo'])
    expect(header('Progress')!.attributes('aria-sort')).toBe('ascending')

    await header('Progress')!.find('button').trigger('click')
    await flushPromises()
    // DEBUG
    console.log('desc rows', JSON.stringify(rowTitles()))
    console.log('progress aria', header('Progress')!.attributes('aria-sort'))
    expect(rowTitles()).toEqual(['Alpha', 'Charlie', 'Bravo'])
    expect(header('Progress')!.attributes('aria-sort')).toBe('descending')

    wrapper.unmount()
  })
})
