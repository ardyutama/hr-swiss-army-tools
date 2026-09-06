import { delJson, getJson, postJson, putJson } from '@/shared/http'

export interface VacancyProgress {
  processedCandidates: number
  totalCandidates: number
}

export interface VacancyHiring {
  neededHires: number
  activeHires: number
}

export interface VacancySummary {
  id: string
  title: string
  openedOn: string
  status: 'open' | 'closed'
  progress: VacancyProgress
  hiring?: VacancyHiring | null
}

export interface VacancyRequirement {
  id: string
  phrase: string
  position: number
}

/** One intake round as embedded in the vacancy details rollup. */
export interface VacancyRound {
  id: number
  roundNumber: number
  name: string | null
  status: 'open' | 'closed'
  closedAt: string | null
  candidateCount: number
}

export interface VacancyDetails {
  id: string
  title: string
  openedOn: string
  status: 'open' | 'closed'
  closedAt: string | null
  createdAt: string
  requirements: VacancyRequirement[]
  rounds: VacancyRound[]
  progress: VacancyProgress
  hiring?: VacancyHiring | null
}

export interface VacancyWritePayload {
  title: string
  openedOn: string
  requirements: string[]
  neededHires: number | null
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
