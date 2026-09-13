import { shallowRef, watch, type Ref } from 'vue'
import { useToast } from '@nuxt/ui/composables/useToast'
import { fieldErrorsOf, firstNonFieldError } from '@/shared/validation'
import {
  importCandidates,
  type ImportCandidatesResponse,
  type ImportFileResult,
  type ImportFileStatus,
} from '@/features/candidates/api'
import { problemMessage, problemMessageText } from '@/shared/problem-details'

export function useCandidateImport(
  vacancyId: Ref<string>,
  roundId: Ref<string>,
  onChanged?: () => Promise<void>,
) {
  const toast = useToast()
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
      const response = await importCandidates(vacancyId.value, roundId.value, files)
      results.value = response.results
      announceResults(response.results)
      return response
    } catch (error) {
      const fieldErrors = fieldErrorsOf(error)
      if (fieldErrors) {
        importError.value = firstNonFieldError(fieldErrors, new Set()) ?? 'Something went wrong'
        return null
      }
      const message = problemMessage(error, 'Something went wrong')
      if (message.kind !== 'failure') {
        await onChanged?.()
      }
      importError.value = problemMessageText(message)
      return null
    } finally {
      importing.value = false
    }
  }

  // A different vacancy or round starts with a clean import slate.
  watch([vacancyId, roundId], () => {
    importing.value = false
    importError.value = null
    results.value = null
  })

  /** Dismisses a visible import error (e.g. when the import dialog closes). */
  function clearError() {
    importError.value = null
  }

  function announceResults(importResults: ImportFileResult[]): void {
    const countOf = (status: ImportFileStatus) =>
      importResults.filter((result) => result.status === status).length
    const imported = countOf('imported')
    const skipped = countOf('skipped')
    const failed = countOf('failed')
    const summary = [
      `${imported} imported`,
      ...(skipped > 0 ? [`${skipped} skipped`] : []),
      ...(failed > 0 ? [`${failed} failed`] : []),
    ].join(', ')
    if (imported > 0) {
      toast.add({ title: `Import complete: ${summary}`, color: 'success' })
    } else {
      toast.add({ title: `No candidates imported: ${summary}`, color: 'error' })
    }
  }

  return { importing, importError, results, importFiles, clearError }
}
