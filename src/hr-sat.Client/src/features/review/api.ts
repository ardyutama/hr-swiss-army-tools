import { getJson, putJson } from '@/shared/http'
import type {
  CandidateHireOutcome,
  CandidateReviewStatus,
  CvDocumentResult,
} from '@/features/candidates/api'

export interface CandidateDetails {
  id: number
  reviewStatus: CandidateReviewStatus
  hireOutcome: CandidateHireOutcome
  promotedFromRoundNumber: number | null
  promotedAt: string | null
  priorApplications: PriorApplication[]
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

export interface PriorApplication {
  roundNumber: number
  roundName: string | null
  reviewStatus: CandidateReviewStatus
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

export interface CandidateOutcomePayload {
  outcome: CandidateHireOutcome
  note?: string
}

function candidatePath(vacancyId: string, roundId: string, candidateId: number): string {
  return `/api/vacancies/${vacancyId}/rounds/${roundId}/candidates/${candidateId}`
}

function normalizeCandidateDetails(details: CandidateDetails): CandidateDetails {
  return {
    ...details,
    priorApplications: details.priorApplications ?? [],
  }
}

export function getCandidateDetails(
  vacancyId: string,
  roundId: string,
  candidateId: number,
): Promise<CandidateDetails> {
  return getJson<CandidateDetails>(candidatePath(vacancyId, roundId, candidateId))
    .then(normalizeCandidateDetails)
}

export function updateCandidateDetails(
  vacancyId: string,
  roundId: string,
  candidateId: number,
  payload: CandidateDetailsPayload,
): Promise<CandidateDetails> {
  return putJson<CandidateDetails>(`${candidatePath(vacancyId, roundId, candidateId)}/details`, payload)
    .then(normalizeCandidateDetails)
}

export function updateCandidateNotes(
  vacancyId: string,
  roundId: string,
  candidateId: number,
  notes: string,
): Promise<CandidateDetails> {
  return putJson<CandidateDetails>(`${candidatePath(vacancyId, roundId, candidateId)}/notes`, { notes })
    .then(normalizeCandidateDetails)
}

export function updateCandidateReview(
  vacancyId: string,
  roundId: string,
  candidateId: number,
  payload: ReviewDecisionPayload,
): Promise<CandidateDetails> {
  return putJson<CandidateDetails>(`${candidatePath(vacancyId, roundId, candidateId)}/review`, payload)
    .then(normalizeCandidateDetails)
}

export function updateCandidateOutcome(
  vacancyId: string,
  roundId: string,
  candidateId: number,
  payload: CandidateOutcomePayload,
): Promise<CandidateDetails> {
  return putJson<CandidateDetails>(`${candidatePath(vacancyId, roundId, candidateId)}/outcome`, payload)
    .then(normalizeCandidateDetails)
}

export function updateCandidateRequirementReview(
  vacancyId: string,
  roundId: string,
  candidateId: number,
  requirementId: number,
  confirmed: boolean,
): Promise<CandidateDetails> {
  return putJson<CandidateDetails>(
    `${candidatePath(vacancyId, roundId, candidateId)}/requirement-reviews/${requirementId}`,
    { confirmed },
  ).then(normalizeCandidateDetails)
}
