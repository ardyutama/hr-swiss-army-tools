import type { CandidateSummary } from './api'

/** Display-only name per the Candidate Display Name rule: typed name, else sender name, else sender email, else email subject. */
export function candidateDisplayName(candidate: CandidateSummary): string {
  return (
    candidate.fullName ??
    candidate.sourceSenderName ??
    candidate.sourceSenderEmail ??
    candidate.sourceSubject ??
    'Unknown candidate'
  )
}
