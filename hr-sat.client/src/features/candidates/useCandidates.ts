import { computed, shallowRef, watch, type Ref } from 'vue'
import { toast } from 'vue-sonner'
import { deleteCandidate, listCandidates, type CandidateSummary } from '@/features/candidates/api'
import { candidateDisplayName } from '@/features/candidates/format'

export type CandidatesViewState = 'loading' | 'error' | 'empty' | 'ready'

export function useCandidates(vacancyId: Ref<string>) {
  const candidates = shallowRef<CandidateSummary[] | null>(null)
  const loadError = shallowRef<string | null>(null)
  const removing = shallowRef(false)
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
    try {
      const list = await listCandidates(vacancyId.value)
      // Ignore stale responses when the route param changed meanwhile.
      if (token !== requestToken) {
        return
      }
      candidates.value = list
    } catch (error) {
      if (token !== requestToken) {
        return
      }
      loadError.value = error instanceof Error ? error.message : 'Failed to load candidates'
    }
  }

  // Reload when the route param changes without leaving the route component.
  watch(
    vacancyId,
    () => {
      candidates.value = null
      loadError.value = null
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
      await deleteCandidate(vacancyId.value, candidate.id)
      toast.success(`Candidate "${candidateDisplayName(candidate)}" deleted successfully`, {
        closeButton: true,
      })
      await load()
    } finally {
      removing.value = false
    }
  }

  return { candidates, loadError, viewState, removing, load, remove }
}
