import { getJson, postJson, putJson } from '@/shared/http'
import type {
  CandidateExtractionStatus,
  CandidateMatch,
  CandidateReviewStatus,
  CandidateSkill,
  CvDocumentResult,
} from '@/features/candidates/api'

/** Typed contract of GET/PUT candidate-details endpoints (CandidateDetailsResponse). */
export interface CandidateDetails {
  id: number
  reviewStatus: CandidateReviewStatus
  extractionStatus: CandidateExtractionStatus
  fullName: string | null
  contactEmail: string | null
  contactPhone: string | null
  notes: string | null
  skills: CandidateSkill[]
  match: CandidateMatch
  sourceSenderName: string | null
  sourceSenderEmail: string | null
  sourceSubject: string | null
  sourceBodyText: string | null
  sourceSentAt: string | null
  sourceOriginalFilename: string
  documents: CvDocumentResult[]
}

export interface ReviewDecisionPayload {
  reviewStatus: CandidateReviewStatus
  notes?: string
}

function candidatePath(vacancyId: string, candidateId: number): string {
  return `/api/vacancies/${vacancyId}/candidates/${candidateId}`
}

export function getCandidateDetails(
  vacancyId: string,
  candidateId: number,
): Promise<CandidateDetails> {
  return getJson<CandidateDetails>(candidatePath(vacancyId, candidateId))
}

export function updateCandidateNotes(
  vacancyId: string,
  candidateId: number,
  notes: string,
): Promise<CandidateDetails> {
  return putJson<CandidateDetails>(`${candidatePath(vacancyId, candidateId)}/notes`, { notes })
}

export function updateCandidateReview(
  vacancyId: string,
  candidateId: number,
  payload: ReviewDecisionPayload,
): Promise<CandidateDetails> {
  return putJson<CandidateDetails>(`${candidatePath(vacancyId, candidateId)}/review`, payload)
}

export function retryCandidateExtraction(
  vacancyId: string,
  candidateId: number,
): Promise<CandidateDetails> {
  return postJson<CandidateDetails>(`${candidatePath(vacancyId, candidateId)}/extract`, undefined)
}
