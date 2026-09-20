import { computed, onScopeDispose, shallowRef, watch, type Ref } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { useToast } from '@nuxt/ui/composables/useToast'
import {
  deleteCandidate,
  listCandidates,
  type CandidateListFilters,
  type CandidateListResponse,
  type CandidateSummary,
} from '@/features/candidates/api'
import {
  candidateFilterQuery,
  candidateFilterStateFromQuery,
  type CandidateFilterState,
} from '@/features/candidates/filter'
import { candidateDisplayName } from '@/features/candidates/format'
import { problemMessage, problemMessageText } from '@/shared/problem-details'

export type CandidatesViewState = 'loading' | 'error' | 'empty' | 'ready'

/** Why the candidate list has nothing to show; drives the empty-state copy in the view. */
export type CandidateListEmptyReason =
  | 'vacancy-closed'
  | 'no-active-round'
  | 'round-closed'
  | 'no-candidates'

/**
 * The rendered-list contract for the candidate section. Beyond the async
 * lifecycle and the lifecycle-driven empty reasons, the server-side list adds
 * three states the filters/pager can produce: `no-matches` (filters exclude
 * everything), `all-screened` (every candidate in the round is screened out
 * and the toggle is off), and `stale-page` (a deep-linked or reclassified page
 * came back empty — recoverable, never an auto-redirect).
 */
export type CandidateListState =
  | { kind: 'loading' }
  | { kind: 'error' }
  | { kind: 'empty'; reason: CandidateListEmptyReason }
  | { kind: 'no-matches' }
  | { kind: 'all-screened' }
  | { kind: 'stale-page' }
  | { kind: 'ready' }

/**
 * Lifecycle facts the list needs from the surrounding flow, passed in explicitly
 * so this feature never imports sibling feature state.
 */
export interface CandidateListContext {
  vacancyClosed: Ref<boolean>
  hasActiveRound: Ref<boolean>
  selectedRoundClosed: Ref<boolean>
}

const SEARCH_DEBOUNCE_MS = 300

function sameQuery(left: Record<string, string>, right: Readonly<Record<string, unknown>>): boolean {
  const leftKeys = Object.keys(left)
  const rightKeys = Object.keys(right)
  return (
    leftKeys.length === rightKeys.length &&
    leftKeys.every((key) => String(right[key] ?? '') === left[key])
  )
}

/**
 * The candidate list flow: a paged, server-filtered fetch whose entire state —
 * status/outcome facets, text query, Received sort, page, and the screened-out
 * toggle — lives in the route query. Any filter change resets the page to 1
 * and refetches; a round switch drops only `page` (HR compares rounds through
 * the same lens). The search box is a local draft debounced into the URL.
 */
export function useCandidates(
  vacancyId: Ref<string>,
  roundId: Ref<string>,
  context: CandidateListContext,
) {
  const toast = useToast()
  const route = useRoute()
  const router = useRouter()
  const response = shallowRef<CandidateListResponse | null>(null)
  const loadError = shallowRef<string | null>(null)
  const pending = shallowRef(false)
  const removing = shallowRef(false)
  let requestToken = 0

  const filters = computed<CandidateFilterState>(() => candidateFilterStateFromQuery(route.query))
  const candidates = computed<CandidateSummary[]>(() => response.value?.items ?? [])

  // The search box edits a draft; the URL (and with it the fetch) follows
  // after a quiet period. An echo of our own push never overwrites the draft —
  // the comparison trims, so an in-flight trailing space survives.
  const searchDraft = shallowRef(filters.value.query)
  let searchTimer: ReturnType<typeof setTimeout> | null = null

  watch(searchDraft, (value) => {
    if (searchTimer !== null) {
      clearTimeout(searchTimer)
    }
    searchTimer = setTimeout(() => {
      searchTimer = null
      const query = value.trim()
      if (query !== filters.value.query) {
        replaceFilters({ query, page: 1 })
      }
    }, SEARCH_DEBOUNCE_MS)
  })

  watch(
    () => filters.value.query,
    (query) => {
      if (query !== searchDraft.value.trim()) {
        searchDraft.value = query
      }
    },
  )

  onScopeDispose(() => {
    if (searchTimer !== null) {
      clearTimeout(searchTimer)
    }
  })

  const viewState = computed<CandidatesViewState>(() => {
    if (loadError.value !== null && response.value === null) {
      return 'error'
    }
    if (response.value === null) {
      return 'loading'
    }
    return response.value.total === 0 && response.value.counts.screenedOut === 0
      ? 'empty'
      : 'ready'
  })

  // The empty reasons follow the domain's read-only order: a Closed Vacancy
  // freezes everything, a vacancy with no Active Round rejects imports, a
  // closed round is read-only, and only then is the list simply empty.
  const listState = computed<CandidateListState>(() => {
    const current = response.value
    if (current === null) {
      return loadError.value === null ? { kind: 'loading' } : { kind: 'error' }
    }
    if (current.items.length > 0) {
      return { kind: 'ready' }
    }
    if (current.page > 1) {
      return { kind: 'stale-page' }
    }
    if (current.filteredTotal === 0 && current.total > 0) {
      return { kind: 'no-matches' }
    }
    if (current.total === 0 && current.counts.screenedOut > 0) {
      return { kind: 'all-screened' }
    }
    if (context.vacancyClosed.value) {
      return { kind: 'empty', reason: 'vacancy-closed' }
    }
    if (!context.hasActiveRound.value) {
      return { kind: 'empty', reason: 'no-active-round' }
    }
    if (context.selectedRoundClosed.value) {
      return { kind: 'empty', reason: 'round-closed' }
    }
    return { kind: 'empty', reason: 'no-candidates' }
  })

  /** Pushes patched filter state into the route query; the watcher refetches. */
  function replaceFilters(patch: Partial<CandidateFilterState>) {
    const next = candidateFilterQuery({ ...filters.value, ...patch })
    if (sameQuery(next, route.query)) {
      return
    }
    void router.replace({ query: next })
  }

  const status = computed({
    get: () => filters.value.status,
    set: (value) =>
      replaceFilters({
        status: value,
        // Outcome only exists under All/Shortlisted; leaving them resets it.
        outcome: value === 'all' || value === 'shortlisted' ? filters.value.outcome : 'any',
        page: 1,
      }),
  })
  const outcome = computed({
    get: () => filters.value.outcome,
    set: (value) => replaceFilters({ outcome: value, page: 1 }),
  })
  const screenedAll = computed({
    get: () => filters.value.screenedAll,
    set: (value) => replaceFilters({ screenedAll: value, page: 1 }),
  })
  const page = computed({
    get: () => filters.value.page,
    // Paging keeps the filters; every other change resets to page 1.
    set: (value) => replaceFilters({ page: value }),
  })
  const receivedSort = computed(() => filters.value.receivedSort)

  function toggleReceivedSort() {
    replaceFilters({
      receivedSort: filters.value.receivedSort === 'oldest' ? 'newest' : 'oldest',
      page: 1,
    })
  }

  function clearFilters() {
    searchDraft.value = ''
    replaceFilters({ status: 'all', outcome: 'any', query: '', page: 1 })
  }

  function backToFirstPage() {
    replaceFilters({ page: 1 })
  }

  async function load() {
    const token = ++requestToken
    loadError.value = null

    if (roundId.value === '') {
      response.value = null
      return
    }

    pending.value = true
    try {
      const current = filters.value
      const request: CandidateListFilters = {
        status: current.status,
        outcome: current.outcome,
        query: current.query,
        sort: current.receivedSort,
        screened: current.screenedAll,
        page: current.page,
      }
      const result = await listCandidates(vacancyId.value, roundId.value, request)
      if (token !== requestToken) {
        return
      }
      response.value = result
    } catch (error) {
      if (token !== requestToken) {
        return
      }
      // A refetch failure with data on screen is transient: keep the list and
      // say so in a toast. Only the first load owns the error view state.
      if (response.value === null) {
        loadError.value = problemMessageText(problemMessage(error, 'Something went wrong'))
      } else {
        toast.add({
          title: "Couldn't refresh candidates",
          description: problemMessageText(problemMessage(error, 'Something went wrong')),
          color: 'error',
        })
      }
    } finally {
      if (token === requestToken) {
        pending.value = false
      }
    }
  }

  watch(
    [vacancyId, roundId, () => route.query],
    () => {
      void load()
    },
    { immediate: true },
  )

  // A round switch drops `page` and only `page`; the other filters persist so
  // HR compares rounds through the same lens. The initial pin (no previous
  // round) keeps a deep-linked page.
  let previousRoundId: string | null = null
  watch(roundId, (current) => {
    const isInitialPin = previousRoundId === null || previousRoundId === ''
    if (!isInitialPin && current !== previousRoundId && filters.value.page !== 1) {
      replaceFilters({ page: 1 })
    }
    previousRoundId = current
  })

  async function remove(candidate: CandidateSummary): Promise<void> {
    removing.value = true
    try {
      await deleteCandidate(vacancyId.value, roundId.value, candidate.id)
      toast.add({
        title: `Candidate "${candidateDisplayName(candidate)}" deleted successfully`,
        color: 'success',
      })
    } finally {
      removing.value = false
    }
  }

  return {
    candidates,
    response,
    pending,
    loadError,
    viewState,
    listState,
    filters,
    status,
    outcome,
    searchDraft,
    receivedSort,
    screenedAll,
    page,
    removing,
    load,
    remove,
    toggleReceivedSort,
    clearFilters,
    backToFirstPage,
  }
}
