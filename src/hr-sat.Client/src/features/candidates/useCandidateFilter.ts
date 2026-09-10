import { computed, shallowRef, type Ref } from 'vue'
import type { CandidateSummary } from './api'
import type { CandidatesViewState } from './useCandidates'
import {
  type CandidateFilterState,
  countByHireOutcome,
  countByReviewStatus,
  filterCandidates,
  sortByReceived,
  type CandidateOutcomeFilter,
  type CandidateStatusFilter,
  type ReceivedSort,
} from './filter'

/** Why the candidate list has nothing to show; drives the empty-state copy in the view. */
export type CandidateListEmptyReason =
  | 'vacancy-closed'
  | 'no-active-round'
  | 'round-closed'
  | 'no-candidates'

/**
 * The rendered-list contract for the candidate section: the async lifecycle
 * supplied by `useCandidates`, plus which empty state is meaningful when the
 * list has nothing to show. A closed vacancy or closed round that still has
 * candidates is `ready` — read-only rendering is the flow's `candidatesReadonly`
 * flag, not a list state.
 */
export type CandidateListState =
  | { kind: 'loading' }
  | { kind: 'error' }
  | { kind: 'empty'; reason: CandidateListEmptyReason }
  | { kind: 'ready' }

/**
 * Lifecycle facts the list needs from the surrounding flow, passed in explicitly
 * so this feature never imports sibling feature state.
 */
export interface CandidateListContext {
  viewState: Ref<CandidatesViewState>
  vacancyClosed: Ref<boolean>
  hasActiveRound: Ref<boolean>
  selectedRoundClosed: Ref<boolean>
}

/**
 * Client-side list view state for the candidate table: review-status filter,
 * free-text search, the Received sort direction, and the list-state union that
 * decides what the candidate section renders. Owns no async lifecycle;
 * `useCandidates` stays the data owner and supplies `context.viewState`.
 */
export function useCandidateFilter(
  candidates: Ref<CandidateSummary[] | null>,
  context: CandidateListContext,
  initial: Partial<CandidateFilterState> = {},
) {
  const status = shallowRef<CandidateStatusFilter>(initial.status ?? 'all')
  const outcome = shallowRef<CandidateOutcomeFilter>(initial.outcome ?? 'any')
  const query = shallowRef(initial.query ?? '')
  const receivedSort = shallowRef<ReceivedSort>(initial.receivedSort ?? 'newest')

  const statusCounts = computed(() => countByReviewStatus(candidates.value ?? []))
  const outcomeCounts = computed(() => countByHireOutcome(candidates.value ?? []))

  const filteredCandidates = computed(() =>
    sortByReceived(
      // Outcome is an independent facet and composes with review status/search.
      // The helper also applies the shortlisted-only scope for non-any values.
      filterCandidates(candidates.value ?? [], status.value, query.value, outcome.value),
      receivedSort.value,
    ),
  )

  // The empty reasons follow the domain's read-only order: a Closed Vacancy
  // freezes everything, a vacancy with no Active Round rejects imports, a
  // closed round is read-only, and only then is the list simply empty.
  const listState = computed<CandidateListState>(() => {
    switch (context.viewState.value) {
      case 'loading':
        return { kind: 'loading' }
      case 'error':
        return { kind: 'error' }
      case 'ready':
        return { kind: 'ready' }
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

  function toggleReceivedSort() {
    receivedSort.value = receivedSort.value === 'oldest' ? 'newest' : 'oldest'
  }

  function clearFilters() {
    status.value = 'all'
    outcome.value = 'any'
    query.value = ''
  }

  return {
    status,
    outcome,
    query,
    receivedSort,
    statusCounts,
    outcomeCounts,
    filteredCandidates,
    listState,
    toggleReceivedSort,
    clearFilters,
  }
}
