import { postFormData } from '@/shared/http'
import { serializeLayoutColumns, type FormLayoutColumn } from '@/features/form-layout/api'

export interface FormImportSummary {
  rowsRead: number
  created: number
  updated: number
  skippedOutdated: number
  priorApplications: number
}

function importFormPath(vacancyId: string, roundId: string): string {
  return `/api/vacancies/${vacancyId}/rounds/${roundId}/candidates/import-form`
}

function formDataWith(file: File): FormData {
  const formData = new FormData()
  formData.append('file', file, file.name)
  return formData
}

export function importFormResponses(
  vacancyId: string,
  roundId: string,
  file: File,
): Promise<FormImportSummary> {
  return postFormData<FormImportSummary>(importFormPath(vacancyId, roundId), formDataWith(file))
}

/**
 * Guided setup / drift re-map: the layout payload upserts the Form Layout in
 * the same call and completes the held file's import (ADR-0013).
 */
export function importFormResponsesWithLayout(
  vacancyId: string,
  roundId: string,
  file: File,
  columns: FormLayoutColumn[],
): Promise<FormImportSummary> {
  const formData = formDataWith(file)
  formData.append('layout', serializeLayoutColumns(columns))
  return postFormData<FormImportSummary>(importFormPath(vacancyId, roundId), formData)
}

/** Drift confirmation: the mapping still holds; the file's headers become the new snapshot. */
export function confirmFormImportDrift(
  vacancyId: string,
  roundId: string,
  file: File,
): Promise<FormImportSummary> {
  const formData = formDataWith(file)
  formData.append('confirmDrift', 'true')
  return postFormData<FormImportSummary>(importFormPath(vacancyId, roundId), formData)
}

