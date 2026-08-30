import { computed, onMounted, shallowRef } from 'vue'
import { deleteVacancy, listVacancies, type VacancySummary } from './api'

export type VacanciesViewState = 'loading' | 'error' | 'empty' | 'ready'

export function useVacancies() {
  const vacancies = shallowRef<VacancySummary[] | null>(null)
  const loadError = shallowRef<string | null>(null)

  const loading = computed(() => vacancies.value === null && loadError.value === null)

  const viewState = computed<VacanciesViewState>(() => {
    if (loading.value) {
      return 'loading'
    }
    if (loadError.value !== null) {
      return 'error'
    }
    return (vacancies.value ?? []).length === 0 ? 'empty' : 'ready'
  })

  const openCount = computed(
    () => (vacancies.value ?? []).filter((v) => v.status === 'open').length,
  )

  const cvsToSort = computed(() =>
    (vacancies.value ?? []).reduce(
      (sum, v) => sum + Math.max(0, v.progress.totalCandidates - v.progress.processedCandidates),
      0,
    ),
  )

  async function load() {
    loadError.value = null
    try {
      vacancies.value = await listVacancies()
    } catch (error) {
      loadError.value = error instanceof Error ? error.message : 'Failed to load vacancies'
    }
  }

  async function remove(id: string) {
    await deleteVacancy(id)
    await load()
  }

  onMounted(load)

  return { vacancies, loadError, loading, viewState, openCount, cvsToSort, load, remove }
}
