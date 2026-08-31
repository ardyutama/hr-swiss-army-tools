import { shallowRef, watch, type Ref } from 'vue'
import { toast } from 'vue-sonner'
import { fieldErrorsOf, firstNonFieldError } from '@/shared/validation'
import {
  importCandidates,
  type ImportCandidatesResponse,
  type ImportFileResult,
  type ImportFileStatus,
} from './api'

export function useCandidateImport(vacancyId: Ref<string>) {
  const importing = shallowRef(false)
  const importError = shallowRef<string | null>(null)
  const results = shallowRef<ImportFileResult[] | null>(null)

  async function importFiles(files: File[]): Promise<ImportCandidatesResponse | null> {
    if (files.length === 0 || importing.value) {
      return null
    }
    importing.value = true
    importError.value = null
    try {
      const response = await importCandidates(vacancyId.value, files)
      results.value = response.results
      announceResults(response.results)
      return response
    } catch (error) {
      importError.value = describeError(error)
      return null
    } finally {
      importing.value = false
    }
  }

  // A different vacancy starts with a clean import slate.
  watch(vacancyId, () => {
    importing.value = false
    importError.value = null
    results.value = null
  })

  /** Dismisses a visible import error (e.g. when the import dialog closes). */
  function clearError() {
    importError.value = null
  }

  return { importing, importError, results, importFiles, clearError }
}

function announceResults(results: ImportFileResult[]): void {
  const countOf = (status: ImportFileStatus) =>
    results.filter((result) => result.status === status).length
  const imported = countOf('imported')
  const skipped = countOf('skipped')
  const failed = countOf('failed')
  const summary = [
    `${imported} imported`,
    ...(skipped > 0 ? [`${skipped} skipped`] : []),
    ...(failed > 0 ? [`${failed} failed`] : []),
  ].join(', ')
  if (imported > 0) {
    toast.success(`Import complete: ${summary}`, { closeButton: true })
  } else {
    toast.error(`No candidates imported: ${summary}`, { closeButton: true })
  }
}

function describeError(error: unknown): string {
  const fieldErrors = fieldErrorsOf(error)
  const validationMessage = fieldErrors
    ? firstNonFieldError(fieldErrors, new Set())
    : undefined
  return (
    validationMessage ??
    (error instanceof Error ? error.message : 'Failed to import files')
  )
}
