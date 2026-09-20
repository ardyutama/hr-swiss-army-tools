import { getJson, postJson } from '@/shared/http'
import type { CandidateSummary } from '@/features/candidates/api'

export interface PromoteCandidatesRequest {
  sourceRoundId: number
  candidateIds: number[]
}

/**
 * The source round's unpaged eligible list for the promote dialog. Screened-out
 * candidates are included — the dialog is the rescue path for a fixed rule.
 */
export function getPromoteSummary(
  vacancyId: string,
  sourceRoundId: number,
): Promise<CandidateSummary[]> {
  return getJson<CandidateSummary[]>(
    `/api/vacancies/${vacancyId}/rounds/${sourceRoundId}/promote-summary`,
  )
}

export function promoteCandidates(
  vacancyId: string,
  roundId: number,
  request: PromoteCandidatesRequest,
): Promise<CandidateSummary[]> {
  return postJson<CandidateSummary[]>(
    `/api/vacancies/${vacancyId}/rounds/${roundId}/promotions`,
    request,
  )
}