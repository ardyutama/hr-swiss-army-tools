import { postFormData } from '@/shared/http'

export interface FormImportSummary {
  rowsRead: number
  created: number
  updated: number
  skippedOutdated: number
  priorApplications: number
}

export function importFormResponses(
  vacancyId: string,
  roundId: string,
  file: File,
): Promise<FormImportSummary> {
  const formData = new FormData()
  formData.append('file', file, file.name)
  return postFormData<FormImportSummary>(
    `/api/vacancies/${vacancyId}/rounds/${roundId}/candidates/import-form`,
    formData,
  )
}
