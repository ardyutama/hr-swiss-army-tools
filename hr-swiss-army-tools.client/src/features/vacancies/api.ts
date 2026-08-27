import { getJson } from '@/shared/http'

export interface VacancySummary {
  id: string
  title: string
  createdOn: string
}

export function listVacancies(): Promise<VacancySummary[]> {
  return getJson<VacancySummary[]>('/api/vacancies')
}
