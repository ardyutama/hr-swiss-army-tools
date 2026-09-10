import type { CandidateHireOutcome, CandidateReviewStatus, CandidateSummary } from './api'
import { candidateDisplayName } from './format'

export type CandidateStatusFilter = 'all' | CandidateReviewStatus
export type CandidateOutcomeFilter =
  | 'any'
  | 'undecided'
  | Exclude<CandidateHireOutcome, 'none'>

/** Sort direction for the Received column: source sent-at, oldest first by default. */
export type ReceivedSort = 'oldest' | 'newest'

export interface CandidateFilterState {
  status: CandidateStatusFilter
  outcome: CandidateOutcomeFilter
  query: string
  receivedSort: ReceivedSort
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
  return {
    status,
    // Outcome only exists under Shortlisted (or All); other statuses sanitize to 'any'.
    outcome: status === 'all' || status === 'shortlisted' ? rawOutcome : 'any',
    query: queryString(query.query),
    receivedSort: queryString(query.sort) === 'oldest' ? 'oldest' : 'newest',
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
  return query
}

/** True when the query matches the display name, the source sender email, or the email subject. */
export function matchesCandidateQuery(candidate: CandidateSummary, query: string): boolean {
  const needle = query.trim().toLowerCase()
  if (needle === '') {
    return true
  }
  return [
    candidateDisplayName(candidate),
    candidate.sourceSenderEmail,
    candidate.sourceSubject,
  ].some((value) => value?.toLowerCase().includes(needle))
}

/** Status filter and search combine with AND semantics. */
export function filterCandidates(
  candidates: CandidateSummary[],
  status: CandidateStatusFilter,
  query: string,
  outcome: CandidateOutcomeFilter = 'any',
): CandidateSummary[] {
  return candidates.filter(
    (candidate) =>
      (status === 'all' || candidate.reviewStatus === status) &&
      (outcome === 'any' ||
        (candidate.reviewStatus === 'shortlisted' &&
          (outcome === 'undecided'
            ? candidate.hireOutcome === 'none'
            : candidate.hireOutcome === outcome))) &&
      matchesCandidateQuery(candidate, query),
  )
}

/** Counts outcome facets from the selected round, independent of other filters. */
export function countByHireOutcome(
  candidates: CandidateSummary[],
): Record<CandidateOutcomeFilter, number> {
  const counts: Record<CandidateOutcomeFilter, number> = {
    any: candidates.length,
    undecided: 0,
    hired: 0,
    runaway: 0,
    declined: 0,
  }
  for (const candidate of candidates) {
    if (candidate.reviewStatus !== 'shortlisted') {
      continue
    }
    if (candidate.hireOutcome === 'none') {
      counts.undecided += 1
    } else {
      counts[candidate.hireOutcome] += 1
    }
  }
  return counts
}

/** Live counts per review status for the filter chips. */
export function countByReviewStatus(
  candidates: CandidateSummary[],
): Record<CandidateReviewStatus, number> {
  const counts: Record<CandidateReviewStatus, number> = {
    new: 0,
    flagged: 0,
    shortlisted: 0,
    rejected: 0,
  }
  for (const candidate of candidates) {
    counts[candidate.reviewStatus] += 1
  }
  return counts
}

/** Stable sort by source sent-at; candidates without a date always sink to the bottom. */
export function sortByReceived(
  candidates: CandidateSummary[],
  direction: ReceivedSort,
): CandidateSummary[] {
  const sign = direction === 'oldest' ? 1 : -1
  return [...candidates].sort((a, b) => {
    if (a.sourceSentAt === null || b.sourceSentAt === null) {
      if (a.sourceSentAt === b.sourceSentAt) {
        return a.id - b.id
      }
      return a.sourceSentAt === null ? 1 : -1
    }
    const diff = Date.parse(a.sourceSentAt) - Date.parse(b.sourceSentAt)
    return diff !== 0 ? diff * sign : a.id - b.id
  })
}
