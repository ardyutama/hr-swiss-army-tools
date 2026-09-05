import { computed, onMounted, shallowRef } from 'vue'
import { useToast } from '@nuxt/ui/composables/useToast'
import { fieldErrorsOf, firstNonFieldError } from '@/shared/validation'
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

export function useVacancies() {
  const toast = useToast()
  const vacancies = shallowRef<VacancySummary[] | null>(null)
  const loadError = shallowRef<string | null>(null)
  const saving = shallowRef(false)
  const removing = shallowRef(false)
  const editingDetails = shallowRef<VacancyDetails | null>(null)
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

  /**
   * Starts an edit prefill for the form dialog (`null` cancels a pending one).
   * Details are best-effort: when the fetch fails, the summary-backed form stays
   * editable. The latest call wins, so a stale response never fills the form.
   */
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

  /**
   * Creates or updates a vacancy, then reloads the list so it cannot drift from
   * the server. Re-throws failures so the view can route server field errors
   * back into the form dialog.
   */
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
      toast.add({ title: saveErrorMessage(error, editingId !== null), color: 'error' })
      throw error
    } finally {
      saving.value = false
    }
  }

  /**
   * Deletes a vacancy, then reloads the list. Re-throws failures without a toast
   * so the confirm dialog can keep the failure visible inline.
   */
  async function remove(vacancy: VacancySummary): Promise<void> {
    removing.value = true
    try {
      await deleteVacancy(vacancy.id)
      toast.add({ title: `Vacancy "${vacancy.title}" deleted successfully`, color: 'success' })
      await load()
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
    openCount,
    cvsToSort,
    saving,
    removing,
    editingDetails,
    load,
    beginEdit,
    save,
    remove,
  }
}

function saveErrorMessage(error: unknown, isEdit: boolean): string {
  const serverErrors = fieldErrorsOf(error)
  if (serverErrors) {
    return (
      firstNonFieldError(serverErrors, vacancyFormFieldKeys) ??
      (isEdit ? 'Failed to update vacancy.' : 'Failed to create vacancy.')
    )
  }
  return error instanceof Error ? error.message : 'Something went wrong.'
}
