import type { CandidateDetails, CandidateFormResponse, PriorApplication } from './api'
import type { FormLayoutDto } from '@/features/form-layout/api'
import { truncateHeader } from '@/features/form-layout/format'

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

/** The current stored Form Response, when the candidate has one. */
export function currentFormResponse(candidate: CandidateDetails): CandidateFormResponse | null {
  return candidate.formResponses.find((response) => response.isCurrent) ?? null
}

/**
 * One read-only row of the Form Answers panel: a picked, role-less column with
 * its raw cell. The label is the Column Label when set, else the truncated
 * header snapshot; the other shows muted beside it (layout verifiability rule).
 */
export interface FormAnswerRow {
  ordinal: number
  label: string
  header: string | null
  value: string
}

/** Form Answers in column order — role-bound columns surface elsewhere (details, CV link). */
export function formAnswerRows(
  layout: FormLayoutDto | null,
  response: CandidateFormResponse | null,
): FormAnswerRow[] {
  if (layout === null || response === null) {
    return []
  }

  return layout.columns
    .filter((column) => column.role === null)
    .sort((left, right) => left.ordinal - right.ordinal)
    .map((column) => {
      const header = truncateHeader(layout.headerSnapshot[column.ordinal] ?? '')
      const label = column.label?.trim()
      return {
        ordinal: column.ordinal,
        label: label || header,
        header: label ? header : null,
        value: response.cells[column.ordinal] ?? '',
      }
    })
}

/**
 * The form candidate's CV link: the current response's cell under the layout's
 * CV Link ordinal, trimmed; null when unbound, empty, or not a form candidate's
 * evidence. Mirrors the domain's Candidate.CvLink — the client never re-parses
 * the raw row beyond this read.
 */
export function formCvLink(
  layout: FormLayoutDto | null,
  response: CandidateFormResponse | null,
): string | null {
  const ordinal = layout?.columns.find((column) => column.role === 'cvLink')?.ordinal
  if (ordinal === undefined || response === null) {
    return null
  }

  const value = response.cells[ordinal]?.trim()
  return value ? value : null
}

/** The form Timestamp as system data: parsed when possible, else the raw cell text. */
export function formSubmittedAt(response: CandidateFormResponse | null): string | null {
  if (response === null) {
    return null
  }

  return response.formTimestampParsed ?? response.formTimestampRaw
}
