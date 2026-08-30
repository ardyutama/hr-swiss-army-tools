import { flushPromises, mount } from '@vue/test-utils'
import { afterEach, describe, expect, it, vi } from 'vitest'

import VacancyListView from './VacancyListView.vue'

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
  vi.unstubAllGlobals()
  document.body.innerHTML = ''
})

describe('VacancyListView', () => {
  it('shows the empty state when the API returns no vacancies', async () => {
    vi.stubGlobal('fetch', vi.fn().mockResolvedValue(jsonResponse([])))

    const wrapper = mount(VacancyListView)
    await flushPromises()

    expect(wrapper.text()).toContain('No vacancies yet')
  })

  it('lists vacancies returned by the API', async () => {
    vi.stubGlobal(
      'fetch',
      vi.fn().mockResolvedValue(jsonResponse([vacancy()])),
    )

    const wrapper = mount(VacancyListView)
    await flushPromises()

    expect(wrapper.text()).toContain('Welder')
  })

  it('shows an error state with retry when the API fails', async () => {
    vi.stubGlobal(
      'fetch',
      vi.fn().mockResolvedValue(jsonResponse({ title: 'Server error' }, 500)),
    )

    const wrapper = mount(VacancyListView)
    await flushPromises()

    expect(wrapper.text()).toContain("Couldn't load vacancies")
    expect(wrapper.text()).toContain('Try again')
  })

  it('shows the vacancy status badge from the API status', async () => {
    vi.stubGlobal(
      'fetch',
      vi.fn().mockResolvedValue(jsonResponse([vacancy({ status: 'closed' })])),
    )

    const wrapper = mount(VacancyListView)
    await flushPromises()

    expect(wrapper.text()).toContain('Closed')
  })

  it('keeps the list visible and shows the error inside the dialog when delete fails', async () => {
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

  it('prefills the form from the vacancy details when editing', async () => {
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
      document.body.querySelectorAll<HTMLInputElement>('.vform__requirement-row input'),
    ).map((input) => input.value)
    expect(requirementValues).toEqual(['MIG welding', 'Blueprint reading'])

    wrapper.unmount()
  })
})
