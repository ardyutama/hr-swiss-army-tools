import { computed, shallowRef, type Ref } from 'vue'
import type { CandidateSummary } from './api'
import {
  countByReviewStatus,
  filterCandidates,
  sortByReceived,
  type CandidateStatusFilter,
  type ReceivedSort,
} from './filter'

/**
 * Client-side list view state for the candidate table: review-status filter, free-text
 * search, and the Received sort direction. Owns no async lifecycle; `useCandidates`
 * stays the data owner.
 */
export function useCandidateFilter(candidates: Ref<CandidateSummary[] | null>) {
  const status = shallowRef<CandidateStatusFilter>('all')
  const query = shallowRef('')
  const receivedSort = shallowRef<ReceivedSort>('oldest')

  const statusCounts = computed(() => countByReviewStatus(candidates.value ?? []))

  const filteredCandidates = computed(() =>
    sortByReceived(
      filterCandidates(candidates.value ?? [], status.value, query.value),
      receivedSort.value,
    ),
  )

  function toggleReceivedSort() {
    receivedSort.value = receivedSort.value === 'oldest' ? 'newest' : 'oldest'
  }

  function clearFilters() {
    status.value = 'all'
    query.value = ''
  }

  return {
    status,
    query,
    receivedSort,
    statusCounts,
    filteredCandidates,
    toggleReceivedSort,
    clearFilters,
  }
}
