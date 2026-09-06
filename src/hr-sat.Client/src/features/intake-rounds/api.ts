import { postJson, putJson } from '@/shared/http'

/** One intake round as returned by the round create/close endpoints. */
export interface IntakeRound {
  id: number
  vacancyId: number
  roundNumber: number
  name: string | null
  status: 'open' | 'closed'
  closedAt: string | null
  candidateCount: number
}

export function createRound(vacancyId: string, name: string | null): Promise<IntakeRound> {
  return postJson<IntakeRound>(`/api/vacancies/${vacancyId}/rounds`, { name })
}

export function closeRound(vacancyId: string, roundId: number): Promise<IntakeRound> {
  return putJson<IntakeRound>(`/api/vacancies/${vacancyId}/rounds/${roundId}/close`, {})
}
