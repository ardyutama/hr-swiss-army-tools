import { shallowRef, watch, type Ref } from 'vue'
import { fieldErrorsOf, firstNonFieldError } from '@/shared/validation'
import {
  importCandidates,
  type ImportCandidatesResponse,
  type ImportFileResult,
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

  return { importing, importError, results, importFiles }
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
