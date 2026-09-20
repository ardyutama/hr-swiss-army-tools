import type { CandidateHireOutcome, CandidateReviewStatus } from './api'

export type CandidateStatusFilter = 'all' | CandidateReviewStatus
export type CandidateOutcomeFilter =
  | 'any'
  | 'undecided'
  | Exclude<CandidateHireOutcome, 'none'>

/** Sort direction for the Received column: source sent-at, newest first by default. */
export type ReceivedSort = 'oldest' | 'newest'

/**
 * The URL-owned candidate list state. Filtering, sorting, and counting happen
 * server-side (the paged list contract owns them); this module only parses and
 * serializes the route query. Defaults are omitted: `page` is omitted at 1 and
 * `screened` only ever appears as `screened=all`.
 */
export interface CandidateFilterState {
  status: CandidateStatusFilter
  outcome: CandidateOutcomeFilter
  query: string
  receivedSort: ReceivedSort
  page: number
  screenedAll: boolean
}

const candidateStatusFilters: CandidateStatusFilter[] = [
  'all',
  'new',
  'flagged',
  'shortlisted',
  'rejected',
]

const candidateOutcomeFilters: CandidateOutcomeFilter[] = [
  'any',
  'undecided',
  'hired',
  'runaway',
  'declined',
]

function queryString(value: unknown): string {
  if (Array.isArray(value)) {
    return queryString(value[0])
  }
  return typeof value === 'string' ? value : ''
}

/** Reads the list state from route query values, falling back to list defaults. */
export function candidateFilterStateFromQuery(
  query: Readonly<Record<string, unknown>>,
): CandidateFilterState {
  const statusValue = queryString(query.status)
  const outcomeValue = queryString(query.outcome)
  const status = candidateStatusFilters.includes(statusValue as CandidateStatusFilter)
    ? (statusValue as CandidateStatusFilter)
    : 'all'
  const rawOutcome = candidateOutcomeFilters.includes(outcomeValue as CandidateOutcomeFilter)
    ? (outcomeValue as CandidateOutcomeFilter)
    : 'any'
  const parsedPage = Number.parseInt(queryString(query.page), 10)
  return {
    status,
    // Outcome only exists under Shortlisted (or All); other statuses sanitize to 'any'.
    outcome: status === 'all' || status === 'shortlisted' ? rawOutcome : 'any',
    query: queryString(query.query),
    receivedSort: queryString(query.sort) === 'oldest' ? 'oldest' : 'newest',
    page: Number.isFinite(parsedPage) && parsedPage > 1 ? parsedPage : 1,
    screenedAll: queryString(query.screened) === 'all',
  }
}

/** Serializes non-default list state so review navigation preserves the visible queue. */
export function candidateFilterQuery(filters: CandidateFilterState): Record<string, string> {
  const query: Record<string, string> = {}
  if (filters.status !== 'all') {
    query.status = filters.status
  }
  if (filters.outcome !== 'any') {
    query.outcome = filters.outcome
  }
  if (filters.query.trim() !== '') {
    query.query = filters.query.trim()
  }
  if (filters.receivedSort !== 'newest') {
    query.sort = filters.receivedSort
  }
  if (filters.page > 1) {
    query.page = String(filters.page)
  }
  if (filters.screenedAll) {
    query.screened = 'all'
  }
  return query
}
