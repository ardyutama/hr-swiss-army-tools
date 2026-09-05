import { getJson, putJson } from '@/shared/http'
import type { CandidateReviewStatus, CvDocumentResult } from '@/features/candidates/api'

export interface CandidateDetails {
  id: number
  reviewStatus: CandidateReviewStatus
  fullName: string | null
  contactEmail: string | null
  notes: string | null
  requirementReviews: CandidateRequirementReview[]
  sourceSenderName: string | null
  sourceSenderEmail: string | null
  sourceSubject: string | null
  sourceBodyText: string | null
  sourceSentAt: string | null
  sourceOriginalFilename: string
  documents: CvDocumentResult[]
}

export interface CandidateRequirementReview {
  requirementId: number
  confirmed: boolean
}

export interface CandidateDetailsPayload {
  fullName: string
  contactEmail: string
}

export interface ReviewDecisionPayload {
  reviewStatus: CandidateReviewStatus
  notes: string
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

export function updateCandidateDetails(
  vacancyId: string,
  candidateId: number,
  payload: CandidateDetailsPayload,
): Promise<CandidateDetails> {
  return putJson<CandidateDetails>(`${candidatePath(vacancyId, candidateId)}/details`, payload)
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

export function updateCandidateRequirementReview(
  vacancyId: string,
  candidateId: number,
  requirementId: number,
  confirmed: boolean,
): Promise<CandidateDetails> {
  return putJson<CandidateDetails>(
    `${candidatePath(vacancyId, candidateId)}/requirement-reviews/${requirementId}`,
    { confirmed },
  )
}
