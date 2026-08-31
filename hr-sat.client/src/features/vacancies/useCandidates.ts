import { computed, shallowRef, watch, type Ref } from 'vue'
import { listCandidates, type CandidateSummary } from './api'

export type CandidatesViewState = 'loading' | 'error' | 'empty' | 'ready'

export function useCandidates(vacancyId: Ref<string>) {
  const candidates = shallowRef<CandidateSummary[] | null>(null)
  const loadError = shallowRef<string | null>(null)
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

  return { candidates, loadError, viewState, load }
}
