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

afterEach(() => {
  vi.unstubAllGlobals()
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
})
