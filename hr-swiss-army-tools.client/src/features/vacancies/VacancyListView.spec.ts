import { flushPromises, mount } from '@vue/test-utils'
import { afterEach, describe, expect, it, vi } from 'vitest'

import VacancyListView from './VacancyListView.vue'

function jsonResponse(body: unknown, status = 200): Response {
  return new Response(JSON.stringify(body), {
    status,
    headers: { 'Content-Type': 'application/json' },
  })
}

afterEach(() => {
  vi.unstubAllGlobals()
})

describe('VacancyListView', () => {
  it('shows the empty state when the API returns no vacancies', async () => {
    vi.stubGlobal('fetch', vi.fn().mockResolvedValue(jsonResponse([])))

    const wrapper = mount(VacancyListView)
    await flushPromises()

    expect(wrapper.text()).toContain('No vacancies yet.')
  })

  it('lists vacancies returned by the API', async () => {
    vi.stubGlobal(
      'fetch',
      vi.fn().mockResolvedValue(
        jsonResponse([{ id: '1', title: 'Welder', createdOn: '2026-08-27T00:00:00Z' }]),
      ),
    )

    const wrapper = mount(VacancyListView)
    await flushPromises()

    expect(wrapper.text()).toContain('Welder')
  })
})
