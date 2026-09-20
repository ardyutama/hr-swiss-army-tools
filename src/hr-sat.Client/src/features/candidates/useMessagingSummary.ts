import { shallowRef, watch, type Ref } from 'vue'
import { getMessagingSummary, type CandidateSummary } from '@/features/candidates/api'
import { problemMessage, problemMessageText } from '@/shared/problem-details'

/**
 * The round's full unpaged candidate list for the messaging flows (prepared
 * messages and template preview). Loads eagerly on round change so the dialogs
 * read an already-loaded list, exactly as they did over the paged list before.
 * Screening never filters this list: screened-out candidates stay Review
 * Status `new` and are already non-contactable.
 */
export function useMessagingSummary(vacancyId: Ref<string>, roundId: Ref<string>) {
  const summaries = shallowRef<CandidateSummary[] | null>(null)
  const loadError = shallowRef<string | null>(null)
  let requestToken = 0

  async function load() {
    const token = ++requestToken
    loadError.value = null

    if (roundId.value === '') {
      summaries.value = null
      return
    }

    try {
      const list = await getMessagingSummary(vacancyId.value, roundId.value)
      if (token !== requestToken) {
        return
      }
      summaries.value = list
    } catch (error) {
      if (token !== requestToken) {
        return
      }
      loadError.value = problemMessageText(problemMessage(error, 'Something went wrong'))
    }
  }

  watch(
    [vacancyId, roundId],
    () => {
      void load()
    },
    { immediate: true },
  )

  return { summaries, loadError, load }
}
