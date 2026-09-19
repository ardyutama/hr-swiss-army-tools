import { flushPromises } from '@vue/test-utils'
import { describe, expect, it, vi } from 'vitest'

import { jsonResponse, mountView, stubFetch, toastAdd, vacancyDetails } from './mountVacancyDetail'

vi.mock('@nuxt/ui/composables/useToast', () => ({
  useToast: () => ({ add: toastAdd }),
}))

describe('VacancyDetailView', () => {
  it('domain: a vacancy-detail load failure uses friendly retry copy', async () => {
    stubFetch(
      () => vacancyDetails(),
      (url) => {
        if (url.endsWith('/vacancies/1')) {
          return Promise.resolve(jsonResponse({ title: 'Server error' }, 500))
        }
        return undefined
      },
    )

    const { wrapper } = mountView()
    await flushPromises()

    expect(wrapper.find('[role="alert"]').text()).toContain('Something went wrong')
    expect(wrapper.find('[role="alert"]').text()).toContain('Please try again.')
    wrapper.unmount()
  })
})
