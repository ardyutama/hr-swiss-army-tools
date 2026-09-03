import type { CandidateReviewStatus, CandidateSummary } from './api'
import { candidateDisplayName } from './format'

export type CandidateStatusFilter = 'all' | CandidateReviewStatus

/** Sort direction for the Received column: source sent-at, oldest first by default. */
export type ReceivedSort = 'oldest' | 'newest'

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
): CandidateSummary[] {
  return candidates.filter(
    (candidate) =>
      (status === 'all' || candidate.reviewStatus === status) &&
      matchesCandidateQuery(candidate, query),
  )
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
