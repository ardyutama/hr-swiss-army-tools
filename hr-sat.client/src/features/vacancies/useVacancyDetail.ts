import { computed, shallowRef, watch, type Ref } from 'vue'
import { getVacancy, type VacancyDetails } from './api'

export type VacancyDetailViewState = 'loading' | 'error' | 'ready'

export function useVacancyDetail(vacancyId: Ref<string>) {
  const vacancy = shallowRef<VacancyDetails | null>(null)
  const loadError = shallowRef<string | null>(null)
  let requestToken = 0

  const loading = computed(() => vacancy.value === null && loadError.value === null)

  const viewState = computed<VacancyDetailViewState>(() => {
    if (loading.value) {
      return 'loading'
    }
    if (loadError.value !== null) {
      return 'error'
    }
    return 'ready'
  })

  async function load() {
    const token = ++requestToken
    loadError.value = null
    try {
      const details = await getVacancy(vacancyId.value)
      // Ignore stale responses when the route param changed meanwhile.
      if (token !== requestToken) {
        return
      }
      vacancy.value = details
    } catch (error) {
      if (token !== requestToken) {
        return
      }
      loadError.value = error instanceof Error ? error.message : 'Failed to load vacancy'
    }
  }

  // Reload when the route param changes without leaving the route component.
  watch(
    vacancyId,
    () => {
      vacancy.value = null
      loadError.value = null
      void load()
    },
    { immediate: true },
  )

  return { vacancy, loadError, loading, viewState, load }
}
