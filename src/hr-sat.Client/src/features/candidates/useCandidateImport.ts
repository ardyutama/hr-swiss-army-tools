import { shallowRef, watch, type Ref } from 'vue'
import { useToast } from '@nuxt/ui/composables/useToast'
import { firstNonFieldError } from '@/shared/validation'
import {
  importCandidates,
  type ImportFileResult,
  type ImportFileStatus,
} from '@/features/candidates/api'
import { problemMessageText } from '@/shared/problem-details'
import { useImportLifecycle } from '@/shared/useImportLifecycle'

/**
 * The .eml Intake Source adapter: a multi-file upload over the shared import
 * lifecycle. The lifecycle owns the busy flag, the error taxonomy, and the
 * refresh announcement — every terminal outcome that changed server state
 * invokes `onChanged`, so callers never refresh manually; this module keeps
 * only what makes the channel itself: the per-file results and their toast.
 */
export function useCandidateImport(
  vacancyId: Ref<string>,
  roundId: Ref<string>,
  onChanged?: () => Promise<void>,
) {
  const toast = useToast()
  const lifecycle = useImportLifecycle<'candidates', string>({
    vacancyId,
    roundId,
    fallback: 'Something went wrong',
    mapMessage: problemMessageText,
    mapFieldErrors: (errors) => firstNonFieldError(errors, new Set()) ?? 'Something went wrong',
    onChanged,
  })
  const results = shallowRef<ImportFileResult[] | null>(null)

  /** Returns whether the import landed — true means the caller can close the dialog. */
  async function importFiles(files: File[]): Promise<boolean> {
    if (files.length === 0) {
      return false
    }
    const response = await lifecycle.attempt({
      change: 'candidates',
      operation: () => importCandidates(vacancyId.value, roundId.value, files),
      announce: (importResponse) => {
        results.value = importResponse.results
        announceResults(importResponse.results)
      },
    })
    return response !== null
  }

  // A different vacancy or round starts with a clean results slate (the
  // lifecycle resets its own busy flag and alert).
  watch([vacancyId, roundId], () => {
    results.value = null
  })

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

  return {
    importing: lifecycle.busy,
    importError: lifecycle.alert,
    results,
    importFiles,
    clearError: lifecycle.clearAlert,
  }
}
