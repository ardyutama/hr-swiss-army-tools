type CandidateDisplaySource = {
  fullName: string | null
  sourceSenderName: string | null
  sourceSenderEmail: string | null
  sourceSubject?: string | null
}

/** Display-only name per the Candidate Display Name rule: typed name, else sender name, else sender email, else email subject. */
export function candidateDisplayName(candidate: CandidateDisplaySource): string {
  return (
    candidate.fullName ??
    candidate.sourceSenderName ??
    candidate.sourceSenderEmail ??
    candidate.sourceSubject ??
    'Unknown candidate'
  )
}

const receivedFormatter = new Intl.DateTimeFormat(undefined, {
  year: 'numeric',
  month: 'short',
  day: 'numeric',
  hour: 'numeric',
  minute: '2-digit',
})

/** Formats the source email's sent-at timestamp for the Received column. */
export function formatReceivedAt(iso: string | null): string {
  if (iso === null) {
    return '—'
  }
  const date = new Date(iso)
  return Number.isNaN(date.getTime()) ? iso : receivedFormatter.format(date)
}
