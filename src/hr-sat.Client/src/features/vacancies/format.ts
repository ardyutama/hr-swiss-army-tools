import type { CandidateSummary } from '@/features/candidates/api'
import type { VacancyProgress } from './api'

const dateFormatter = new Intl.DateTimeFormat(undefined, {
  year: 'numeric',
  month: 'short',
  day: 'numeric',
})

/** Formats an ISO date for display, falling back to the raw value when it cannot be parsed. */
export function formatDate(iso: string): string {
  const date = new Date(iso)
  return Number.isNaN(date.getTime()) ? iso : dateFormatter.format(date)
}

/** Candidate processing progress as a 0-100 percentage. */
export function progressPercent(progress: VacancyProgress): number {
  if (progress.totalCandidates <= 0) {
    return 0
  }
  return Math.min(100, Math.round((progress.processedCandidates / progress.totalCandidates) * 100))
}

/** Best available display name until CV extraction fills in the candidate details. */
export function candidateDisplayName(candidate: CandidateSummary): string {
  return (
    candidate.fullName ??
    candidate.sourceSenderName ??
    candidate.sourceSenderEmail ??
    'Unknown candidate'
  )
}
