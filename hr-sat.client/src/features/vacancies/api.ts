import { delJson, getJson, postJson, putJson } from '@/shared/http'

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
