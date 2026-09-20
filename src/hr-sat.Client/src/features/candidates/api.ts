import { delJson, getJson, postFormData } from '@/shared/http'

export type CandidateReviewStatus = 'new' | 'flagged' | 'shortlisted' | 'rejected'
export type CandidateHireOutcome = 'none' | 'hired' | 'runaway' | 'declined'

export interface CvDocumentResult {
  id: number
  originalFilename: string
  sizeBytes: number
  isPrimary: boolean
  downloadUrl: string
}

export interface ImportedCandidate {
  id: number
  reviewStatus: CandidateReviewStatus
  hireOutcome: CandidateHireOutcome
  sourceSenderName: string | null
  sourceSenderEmail: string | null
  sourceSubject: string | null
  sourceBodyText: string | null
  sourceSentAt: string | null
  sourceOriginalFilename: string
  documents: CvDocumentResult[]
}

export type ImportFileStatus = 'imported' | 'skipped' | 'failed'

export interface ImportFileResult {
  fileName: string
  status: ImportFileStatus
  error: string | null
  candidate: ImportedCandidate | null
}

export interface ImportCandidatesResponse {
  results: ImportFileResult[]
}

export function importCandidates(
  vacancyId: string,
  roundId: string,
  files: File[],
): Promise<ImportCandidatesResponse> {
  const formData = new FormData()
  for (const file of files) {
    formData.append('files', file, file.name)
  }
  return postFormData<ImportCandidatesResponse>(
    `/api/vacancies/${vacancyId}/rounds/${roundId}/candidates/import`,
    formData,
  )
}

/** A fired Screening Rule as rendered by the server — the chip text is verbatim. */
export interface FiredScreeningRule {
  index: number
  display: string
}

export interface CandidateSummary {
  id: number
  fullName: string | null
  contactEmail: string | null
  notes: string | null
  reviewStatus: CandidateReviewStatus
  hireOutcome: CandidateHireOutcome
  isResubmitted: boolean
  sourceSenderName: string | null
  sourceSenderEmail: string | null
  sourceSubject: string | null
  sourceSentAt: string | null
  cvDocumentCount: number
  intakeSource: string
  cvLink: string | null
  screenedOut: boolean
  firedRules: FiredScreeningRule[]
}

export interface CandidateStatusCounts {
  new: number
  flagged: number
  shortlisted: number
  rejected: number
}

export interface CandidateOutcomeCounts {
  any: number
  undecided: number
  hired: number
  runaway: number
  declined: number
}

/**
 * Server-computed list counts: status/outcome are taken after the screened-out
 * exclusion but independent of the active filters; `screenedOut` is independent
 * of every filter and powers the toolbar toggle badge.
 */
export interface CandidateListCounts {
  status: CandidateStatusCounts
  outcome: CandidateOutcomeCounts
  screenedOut: number
}

/** The paged candidate-list contract (100/page); `total` is the in-scope unfiltered count. */
export interface CandidateListResponse {
  items: CandidateSummary[]
  page: number
  pageSize: number
  total: number
  filteredTotal: number
  counts: CandidateListCounts
}

/** Server-side list filters; omitted values fall back to the endpoint defaults. */
export interface CandidateListFilters {
  status?: string
  outcome?: string
  query?: string
  sort?: string
  screened?: boolean
  page?: number
}

function candidateListQuery(filters: CandidateListFilters): string {
  const params = new URLSearchParams()
  if (filters.status && filters.status !== 'all') {
    params.set('status', filters.status)
  }
  if (filters.outcome && filters.outcome !== 'any') {
    params.set('outcome', filters.outcome)
  }
  if (filters.query && filters.query.trim() !== '') {
    params.set('query', filters.query.trim())
  }
  if (filters.sort && filters.sort !== 'newest') {
    params.set('sort', filters.sort)
  }
  if (filters.screened) {
    params.set('screened', 'all')
  }
  if (filters.page && filters.page > 1) {
    params.set('page', String(filters.page))
  }
  const serialized = params.toString()
  return serialized === '' ? '' : `?${serialized}`
}

export function listCandidates(
  vacancyId: string,
  roundId: string,
  filters: CandidateListFilters = {},
): Promise<CandidateListResponse> {
  return getJson<CandidateListResponse>(
    `/api/vacancies/${vacancyId}/rounds/${roundId}/candidates${candidateListQuery(filters)}`,
  )
}

/**
 * The review queue: the same filters as the paged list (page excluded — the
 * queue is unpaged), so review navigation sees exactly what the list showed.
 */
export function getReviewQueue(
  vacancyId: string,
  roundId: string,
  filters: CandidateListFilters = {},
): Promise<CandidateSummary[]> {
  return getJson<CandidateSummary[]>(
    `/api/vacancies/${vacancyId}/rounds/${roundId}/review-queue${candidateListQuery(filters)}`,
  )
}

/**
 * The messaging summary: the round's full unpaged list with screening fields,
 * so the send flow and template preview never shrink to a 100-row page.
 */
export function getMessagingSummary(
  vacancyId: string,
  roundId: string,
): Promise<CandidateSummary[]> {
  return getJson<CandidateSummary[]>(
    `/api/vacancies/${vacancyId}/rounds/${roundId}/messaging-summary`,
  )
}

export function deleteCandidate(
  vacancyId: string,
  roundId: string,
  candidateId: number,
): Promise<void> {
  return delJson(`/api/vacancies/${vacancyId}/rounds/${roundId}/candidates/${candidateId}`)
}
