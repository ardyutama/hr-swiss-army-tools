import { postJson } from '@/shared/http'
import type { CandidateSummary } from '@/features/candidates/api'

export interface PromoteCandidatesRequest {
  sourceRoundId: number
  candidateIds: number[]
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