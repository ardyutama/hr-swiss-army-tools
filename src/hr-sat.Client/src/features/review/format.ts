import type { PriorApplication } from './api'

/** HH:MM in the user's locale, 24-hour — for the notes "Saved HH:MM" whisper. */
export function formatClockTime(value: Date): string {
  return value.toLocaleTimeString(undefined, {
    hour: '2-digit',
    minute: '2-digit',
    hour12: false,
  })
}

export function formatPromotionHistory(
  promotedFromRoundNumber: number | null | undefined,
  promotedAt: string | null | undefined,
): string | null {
  if (promotedFromRoundNumber == null || promotedAt == null) {
    return null
  }

  const date = new Date(promotedAt)
  const dateLabel = Number.isNaN(date.getTime())
    ? promotedAt.slice(0, 10)
    : date.toISOString().slice(0, 10)
  return `Promoted from Round ${promotedFromRoundNumber} on ${dateLabel}.`
}

const priorReviewStatusLabels: Record<PriorApplication['reviewStatus'], string> = {
  new: 'not yet reviewed',
  flagged: 'flagged',
  shortlisted: 'shortlisted',
  rejected: 'rejected',
}

function formatPriorApplication(application: PriorApplication): string {
  const roundName = application.roundName?.trim()
  const nameSuffix = roundName ? ` (${roundName})` : ''
  return `Round ${application.roundNumber}${nameSuffix} — ${priorReviewStatusLabels[application.reviewStatus]}`
}

export function formatPriorApplications(applications: PriorApplication[]): string | null {
  if (applications.length === 0) {
    return null
  }

  return `This sender also applied in ${applications.map(formatPriorApplication).join('; ')}.`
}

export function formatPriorApplicationAnnouncement(application: PriorApplication): string {
  return `Prior application in Round ${application.roundNumber} — ${priorReviewStatusLabels[application.reviewStatus]}.`
}
