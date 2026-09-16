import { shallowRef, type Ref } from 'vue'
import { useToast } from '@nuxt/ui/composables/useToast'
import type { VacancyDetails } from '@/features/vacancies/api'
import { closeVacancy } from '@/features/vacancies/api'
import { problemMessage } from '@/shared/problem-details'

/**
 * Owns the close-vacancy mutation for the vacancy-detail flow. The vacancy is
 * source-of-truthed by `useVacancyDetail`; this only performs the close and
 * re-runs `onChanged` so the rollup refreshes to the closed (read-only) state.
 */
export function useCloseVacancy(vacancy: Ref<VacancyDetails | null>, onChanged: () => Promise<void>) {
  const toast = useToast()
  const closing = shallowRef(false)

  async function close(): Promise<boolean> {
    const current = vacancy.value
    if (closing.value || !current || current.status !== 'open') {
      return false
    }
    closing.value = true
    try {
      await closeVacancy(current.id)
      toast.add({ title: `Vacancy "${current.title}" closed`, color: 'success' })
      await onChanged()
      return true
    } catch (error) {
      const message = problemMessage(error, "Couldn't close the vacancy")
      toast.add({
        title: message.title,
        description: message.description,
        color: message.color,
      })
      if (message.kind !== 'failure') {
        await onChanged()
      }
      return false
    } finally {
      closing.value = false
    }
  }

  return { closing, close }
}
