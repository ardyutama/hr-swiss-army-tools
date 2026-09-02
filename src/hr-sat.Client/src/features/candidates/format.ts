import type { CandidateSummary } from './api'

/** Best available display name until CV extraction fills in the candidate details. */
export function candidateDisplayName(candidate: CandidateSummary): string {
  return (
    candidate.fullName ??
    candidate.sourceSenderName ??
    candidate.sourceSenderEmail ??
    'Unknown candidate'
  )
}
