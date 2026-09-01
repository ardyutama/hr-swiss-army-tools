import { delJson, getJson, postFormData } from '@/shared/http'

export type CandidateReviewStatus = 'new' | 'flagged' | 'shortlisted' | 'rejected'

export type CandidateExtractionStatus = 'pending' | 'succeeded' | 'failed'

export interface CvDocumentResult {
  id: number
  originalFilename: string
  sizeBytes: number
  isPrimary: boolean
  downloadUrl: string
}

export interface CandidateMatch {
  matchedRequirements: number
  totalRequirements: number
}

export interface CandidateSkill {
  id: number
  phrase: string
  position: number
}

export interface ImportedCandidate {
  id: number
  reviewStatus: CandidateReviewStatus
  extractionStatus: CandidateExtractionStatus
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
  files: File[],
): Promise<ImportCandidatesResponse> {
  const formData = new FormData()
  for (const file of files) {
    formData.append('files', file, file.name)
  }
  return postFormData<ImportCandidatesResponse>(
    `/api/vacancies/${vacancyId}/candidates/import`,
    formData,
  )
}

export interface CandidateSummary {
  id: number
  fullName: string | null
  contactEmail: string | null
  contactPhone: string | null
  notes: string | null
  reviewStatus: CandidateReviewStatus
  extractionStatus: CandidateExtractionStatus
  skills: CandidateSkill[]
  match: CandidateMatch
  sourceSenderName: string | null
  sourceSenderEmail: string | null
  sourceSubject: string | null
  sourceSentAt: string | null
}

export function listCandidates(vacancyId: string): Promise<CandidateSummary[]> {
  return getJson<CandidateSummary[]>(`/api/vacancies/${vacancyId}/candidates`)
}

export function deleteCandidate(vacancyId: string, candidateId: number): Promise<void> {
  return delJson(`/api/vacancies/${vacancyId}/candidates/${candidateId}`)
}
