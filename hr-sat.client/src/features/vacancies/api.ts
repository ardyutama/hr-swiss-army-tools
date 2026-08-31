import { delJson, getJson, postFormData, postJson, putJson } from '@/shared/http'

export interface VacancyProgress {
  processedCandidates: number
  totalCandidates: number
}

export interface VacancySummary {
  id: string
  title: string
  openedOn: string
  status: 'open' | 'closed'
  progress: VacancyProgress
}

export interface VacancyRequirement {
  id: string
  phrase: string
  position: number
}

export interface VacancyDetails {
  id: string
  title: string
  openedOn: string
  status: 'open' | 'closed'
  closedAt: string | null
  createdAt: string
  requirements: VacancyRequirement[]
  progress: VacancyProgress
}

export interface VacancyWritePayload {
  title: string
  openedOn: string
  requirements: string[]
}

export function listVacancies(): Promise<VacancySummary[]> {
  return getJson<VacancySummary[]>('/api/vacancies')
}

export function getVacancy(id: string): Promise<VacancyDetails> {
  return getJson<VacancyDetails>(`/api/vacancies/${id}`)
}

export function createVacancy(payload: VacancyWritePayload): Promise<VacancyDetails> {
  return postJson<VacancyDetails>('/api/vacancies', payload)
}

export function updateVacancy(id: string, payload: VacancyWritePayload): Promise<VacancyDetails> {
  return putJson<VacancyDetails>(`/api/vacancies/${id}`, payload)
}

export function deleteVacancy(id: string): Promise<void> {
  return delJson(`/api/vacancies/${id}`)
}

export type CandidateReviewStatus = 'new' | 'flagged' | 'shortlisted' | 'rejected'

export type CandidateExtractionStatus = 'pending' | 'succeeded' | 'failed'

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
  notes: string | null
  reviewStatus: CandidateReviewStatus
  extractionStatus: CandidateExtractionStatus
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
