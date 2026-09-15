import { computed, onMounted, shallowRef } from 'vue'
import { useToast } from '@nuxt/ui/composables/useToast'
import { fieldErrorsOf, firstNonFieldError } from '@/shared/validation'
import { problemMessage, problemMessageText } from '@/shared/problem-details'
import {
  createVacancy,
  deleteVacancy,
  getVacancy,
  listVacancies,
  updateVacancy,
  type VacancyDetails,
  type VacancySummary,
  type VacancyWritePayload,
} from './api'
import { vacancyFormFieldKeys } from './validation'

export type VacanciesViewState = 'loading' | 'error' | 'empty' | 'ready'

export type VacancyStatusFilter = 'open' | 'closed' | 'all'

export type VacancySortKey = 'title' | 'opened' | 'progress'

export type SortDirection = 'asc' | 'desc'

/** Processed ratio used for progress sorting; a vacancy with no candidates always ranks last. */
function progressRatio(vacancy: VacancySummary): number | null {
  const { processedCandidates, totalCandidates } = vacancy.progress
  return totalCandidates <= 0 ? null : processedCandidates / totalCandidates
}

function compareBy(key: VacancySortKey, a: VacancySummary, b: VacancySummary): number {
  switch (key) {
    case 'title':
      return a.title.localeCompare(b.title)
    case 'opened':
      return a.openedOn.localeCompare(b.openedOn)
    case 'progress': {
      const ra = progressRatio(a)
      const rb = progressRatio(b)
      return (ra ?? 2) - (rb ?? 2)
    }
  }
}

export function useVacancies() {
  const toast = useToast()
  const vacancies = shallowRef<VacancySummary[] | null>(null)
  const loadError = shallowRef<string | null>(null)
  const saving = shallowRef(false)
  const removing = shallowRef(false)
  const editingDetails = shallowRef<VacancyDetails | null>(null)
  const statusFilter = shallowRef<VacancyStatusFilter>('open')
  const searchQuery = shallowRef('')
  const sortKey = shallowRef<VacancySortKey | null>(null)
  const sortDirection = shallowRef<SortDirection>('asc')
  let editRequestToken = 0

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

  const statusCounts = computed(() => {
    const all = vacancies.value ?? []
    return {
      open: all.filter((v) => v.status === 'open').length,
      closed: all.filter((v) => v.status === 'closed').length,
    }
  })

  const filteredVacancies = computed<VacancySummary[]>(() => {
    const query = searchQuery.value.trim().toLowerCase()
    return (vacancies.value ?? []).filter((v) => {
      if (statusFilter.value !== 'all' && v.status !== statusFilter.value) {
        return false
      }
      return query === '' || v.title.toLowerCase().includes(query)
    })
  })

  function clearFilters() {
    statusFilter.value = 'all'
    searchQuery.value = ''
  }

  /** Three-state header cycle: asc → desc → back to the default server order. */
  function toggleSort(key: VacancySortKey) {
    if (sortKey.value !== key) {
      sortKey.value = key
      sortDirection.value = 'asc'
      return
    }
    if (sortDirection.value === 'asc') {
      sortDirection.value = 'desc'
      return
    }
    sortKey.value = null
  }

  const sortedVacancies = computed<VacancySummary[]>(() => {
    const key = sortKey.value
    if (key === null) {
      return filteredVacancies.value
    }
    const direction = sortDirection.value === 'asc' ? 1 : -1
    return [...filteredVacancies.value].sort((a, b) => compareBy(key, a, b) * direction)
  })

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
      loadError.value = problemMessageText(problemMessage(error, 'Something went wrong'))
    }
  }

  function beginEdit(vacancy: VacancySummary | null) {
    const token = ++editRequestToken
    editingDetails.value = null
    if (!vacancy) {
      return
    }
    getVacancy(vacancy.id)
      .then((details) => {
        if (token === editRequestToken) {
          editingDetails.value = details
        }
      })
      .catch(() => {
        // Best-effort prefill; the summary-backed form stays editable.
      })
  }

  async function save(payload: VacancyWritePayload, editingId: string | null): Promise<void> {
    saving.value = true
    try {
      if (editingId) {
        await updateVacancy(editingId, payload)
        toast.add({ title: `Vacancy "${payload.title}" updated successfully`, color: 'success' })
      } else {
        await createVacancy(payload)
        toast.add({ title: 'Vacancy created successfully', color: 'success' })
      }
      await load()
    } catch (error) {
      const serverErrors = fieldErrorsOf(error)
      if (serverErrors) {
        toast.add({
          title:
            firstNonFieldError(serverErrors, vacancyFormFieldKeys) ??
            (editingId !== null ? 'Failed to update vacancy.' : 'Failed to create vacancy.'),
          color: 'error',
        })
        throw error
      }
      const message = problemMessage(
        error,
        editingId !== null ? "Couldn't save vacancy" : "Couldn't create vacancy",
      )
      toast.add({
        title: message.title,
        description: message.description,
        color: message.color,
      })
      if (message.kind !== 'failure') {
        await load()
      }
      throw error
    } finally {
      saving.value = false
    }
  }

  async function remove(vacancy: VacancySummary): Promise<void> {
    removing.value = true
    try {
      await deleteVacancy(vacancy.id)
      toast.add({ title: `Vacancy "${vacancy.title}" purged successfully`, color: 'success' })
      await load()
    } catch (error) {
      const message = problemMessage(error, 'Something went wrong')
      if (message.kind !== 'failure') {
        await load()
      }
      throw error
    } finally {
      removing.value = false
    }
  }

  onMounted(load)

  return {
    vacancies,
    loadError,
    loading,
    viewState,
    cvsToSort,
    statusFilter,
    searchQuery,
    statusCounts,
    filteredVacancies,
    clearFilters,
    sortKey,
    sortDirection,
    toggleSort,
    sortedVacancies,
    saving,
    removing,
    editingDetails,
    load,
    beginEdit,
    save,
    remove,
  }
}
