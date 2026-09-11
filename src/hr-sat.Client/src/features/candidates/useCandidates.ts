import { computed, shallowRef, watch, type Ref } from 'vue'
import { useToast } from '@nuxt/ui/composables/useToast'
import { deleteCandidate, listCandidates, type CandidateSummary } from '@/features/candidates/api'
import { candidateDisplayName } from '@/features/candidates/format'
import { problemMessage, problemMessageText } from '@/shared/problem-details'

export type CandidatesViewState = 'loading' | 'error' | 'empty' | 'ready'

export function useCandidates(vacancyId: Ref<string>, roundId: Ref<string>) {
  const toast = useToast()
  const candidates = shallowRef<CandidateSummary[] | null>(null)
  const loadError = shallowRef<string | null>(null)
  const removing = shallowRef(false)
  const candidateCache = new Map<string, CandidateSummary[]>()
  let requestToken = 0

  const viewState = computed<CandidatesViewState>(() => {
    if (candidates.value === null && loadError.value === null) {
      return 'loading'
    }
    if (loadError.value !== null) {
      return 'error'
    }
    return (candidates.value ?? []).length === 0 ? 'empty' : 'ready'
  })

  async function load() {
    const token = ++requestToken
    loadError.value = null

    if (roundId.value === '') {
      candidates.value = null
      return
    }

    const cacheKey = `${vacancyId.value}:${roundId.value}`
    candidates.value = candidateCache.get(cacheKey) ?? null

    try {
      const list = await listCandidates(vacancyId.value, roundId.value)
      // Ignore stale responses when the route param changed meanwhile.
      if (token !== requestToken) {
        return
      }
      candidateCache.set(cacheKey, list)
      candidates.value = list
    } catch (error) {
      if (token !== requestToken) {
        return
      }
      loadError.value = problemMessageText(problemMessage(error, 'Something went wrong'))
    }
  }

  // Reload when the vacancy or round route param changes without leaving the component.
  watch(
    [vacancyId, roundId],
    () => {
      void load()
    },
    { immediate: true },
  )

  /**
   * Deletes a candidate, then reloads the list. Re-throws failures without a
   * toast so the confirm dialog can keep the failure visible inline.
   */
  async function remove(candidate: CandidateSummary): Promise<void> {
    removing.value = true
    try {
      await deleteCandidate(vacancyId.value, roundId.value, candidate.id)
      toast.add({
        title: `Candidate "${candidateDisplayName(candidate)}" deleted successfully`,
        color: 'success',
      })
      await load()
    } finally {
      removing.value = false
    }
  }

  return { candidates, loadError, viewState, removing, load, remove }
}
