import type { CandidateSummary } from '@/features/candidates/api'

export function mailtoHref(to: string, subject: string, body: string): string {
  const encodedBody = encodeURIComponent(body.replace(/\r\n/g, '\n')).replace(/%0A/g, '%0D%0A')
  return `mailto:${to}?subject=${encodeURIComponent(subject)}&body=${encodedBody}`
}

export function clipboardText(to: string, subject: string, body: string): string {
  return `To: ${to}\nSubject: ${subject}\n\n${body}`
}

export function exclusionSummary(
  contactable: number,
  total: number,
  undecided: number,
  outcomeRecorded: number,
): string | null {
  if (undecided === 0 && outcomeRecorded === 0) {
    return null
  }
  const reasons: string[] = []
  if (undecided > 0) {
    reasons.push(`${undecided} ${undecided === 1 ? 'is' : 'are'} new or flagged`)
  }
  if (outcomeRecorded > 0) {
    reasons.push(
      `${outcomeRecorded} already ${outcomeRecorded === 1 ? 'has' : 'have'} a hire outcome`,
    )
  }
  return `Contacting ${contactable} of ${total} ${total === 1 ? 'candidate' : 'candidates'} — ${reasons.join(', ')}.`
}

export function scopeDescription(candidateCount: number, roundName: string): string {
  return `${candidateCount} ${candidateCount === 1 ? 'candidate' : 'candidates'} in ${roundName}`
}

export function sendScope(
  target: CandidateSummary | null,
  roundCandidates: CandidateSummary[],
): CandidateSummary[] {
  return target === null ? roundCandidates : [target]
}
