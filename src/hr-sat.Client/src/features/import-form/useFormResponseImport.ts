import { computed, shallowRef, watch, type Ref } from 'vue'
import { useToast } from '@nuxt/ui/composables/useToast'
import { ApiError } from '@/shared/http'
import { fieldErrorsOf, firstNonFieldError } from '@/shared/validation'
import { problemMessage, type ProblemMessageColor } from '@/shared/problem-details'
import { importFormResponses, type FormImportSummary } from './api'

export interface FormImportAlert {
  color: ProblemMessageColor
  title: string
  description?: string
}

export function useFormResponseImport(
  vacancyId: Ref<string>,
  roundId: Ref<string>,
  onChanged?: () => Promise<void>,
) {
  const toast = useToast()
  const importing = shallowRef(false)
  const importError = shallowRef<FormImportAlert | null>(null)
  const summary = shallowRef<FormImportSummary | null>(null)

  const summaryLine = computed(() =>
    summary.value ? formImportSummaryLine(summary.value) : null,
  )

  async function importFile(file: File): Promise<boolean> {
    if (importing.value) {
      return false
    }
    importing.value = true
    importError.value = null
    summary.value = null
    try {
      const result = await importFormResponses(vacancyId.value, roundId.value, file)
      summary.value = result
      announce(result)
      return true
    } catch (error) {
      const malformed = malformedCsvAlert(error)
      if (malformed) {
        importError.value = malformed
        return false
      }
      const message = problemMessage(error, "Couldn't import the form responses")
      if (message.kind !== 'failure') {
        await onChanged?.()
      }
      importError.value = {
        color: message.color,
        title: message.title,
        description: message.description,
      }
      return false
    } finally {
      importing.value = false
    }
  }

  // A different vacancy or round starts with a clean import slate.
  watch([vacancyId, roundId], () => {
    importing.value = false
    importError.value = null
    summary.value = null
  })

  /** Dismisses a visible summary or import error (e.g. when the import dialog closes). */
  function clearResult() {
    importError.value = null
    summary.value = null
  }

  function announce(result: FormImportSummary): void {
    toast.add({
      title: `Form import complete: ${formImportSummaryLine(result)}`,
      color: result.created + result.updated > 0 ? 'success' : 'neutral',
    })
  }

  return { importing, importError, summary, summaryLine, importFile, clearResult }
}

function formImportSummaryLine(summary: FormImportSummary): string {
  const segments = [
    summary.rowsRead === 1 ? '1 row read' : `${summary.rowsRead} rows read`,
    ...(summary.created > 0 ? [`${summary.created} new`] : []),
    ...(summary.updated > 0 ? [`${summary.updated} updated (resubmitted)`] : []),
    ...(summary.skippedOutdated > 0 ? [`${summary.skippedOutdated} skipped (outdated)`] : []),
    ...(summary.priorApplications > 0
      ? [`${summary.priorApplications} prior applications noticed`]
      : []),
  ]
  return segments.join(' · ')
}

// A 400 from this endpoint is always a malformed CSV (ADR-0013): the server
// message names the row position and is surfaced verbatim as a red alert.
function malformedCsvAlert(error: unknown): FormImportAlert | null {
  if (!(error instanceof ApiError) || error.status !== 400) {
    return null
  }
  const fieldErrors = fieldErrorsOf(error)
  const detail =
    (fieldErrors ? firstNonFieldError(fieldErrors, new Set()) : undefined) ??
    problemDetail(error.problem) ??
    'The file could not be read.'
  return { color: 'error', title: "Couldn't read that CSV", description: detail }
}

function problemDetail(problem: unknown): string | undefined {
  if (typeof problem !== 'object' || problem === null || !('detail' in problem)) {
    return undefined
  }
  const detail = (problem as { detail?: unknown }).detail
  return typeof detail === 'string' && detail.length > 0 ? detail : undefined
}
