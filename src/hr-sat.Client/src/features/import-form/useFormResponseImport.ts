import { computed, shallowRef, watch, type Ref } from 'vue'
import { useToast } from '@nuxt/ui/composables/useToast'
import { ApiError } from '@/shared/http'
import { fieldErrorsOf, firstNonFieldError } from '@/shared/validation'
import { problemMessage, type ProblemMessageColor } from '@/shared/problem-details'
import type { FormLayoutColumn } from '@/features/form-layout/api'
import type { FormHeaderChangeDto } from '@/features/form-layout/format'
import {
  confirmFormImportDrift,
  importFormResponses,
  importFormResponsesWithLayout,
  type FormImportSummary,
} from './api'

export interface FormImportAlert {
  color: ProblemMessageColor
  title: string
  description?: string
}

/**
 * A refused form import holding its file: `changes` null means the vacancy has
 * no valid Form Layout yet (guided setup); a list means Header Drift. The page
 * watches this to open the matching dialog — the file's single owner is here.
 */
export interface FormImportRefusal {
  file: File
  headers: string[]
  changes: FormHeaderChangeDto[] | null
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
  const pendingRefusal = shallowRef<FormImportRefusal | null>(null)

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
      // The two refusal codes route into the guided/drift dialogs instead of
      // an alert — checked before every other error mapping.
      const refusal = formImportRefusal(error, file)
      if (refusal) {
        pendingRefusal.value = refusal
        return false
      }
      await recordImportError(error)
      return false
    } finally {
      importing.value = false
    }
  }

  /** Guided setup / drift re-map: re-send the held file with the mapping as the layout payload. */
  async function importWithLayout(columns: FormLayoutColumn[]): Promise<boolean> {
    const refusal = pendingRefusal.value
    if (importing.value || refusal === null) {
      return false
    }
    importing.value = true
    importError.value = null
    try {
      const result = await importFormResponsesWithLayout(
        vacancyId.value,
        roundId.value,
        refusal.file,
        columns,
      )
      pendingRefusal.value = null
      summary.value = result
      announce(result)
      return true
    } catch (error) {
      // The refusal stays pending so the dialog remains open with its mapping.
      await recordImportError(error)
      return false
    } finally {
      importing.value = false
    }
  }

  /** Drift confirm: re-send the held file with the confirmDrift flag. */
  async function confirmDrift(): Promise<boolean> {
    const refusal = pendingRefusal.value
    if (importing.value || refusal === null) {
      return false
    }
    importing.value = true
    importError.value = null
    try {
      const result = await confirmFormImportDrift(vacancyId.value, roundId.value, refusal.file)
      pendingRefusal.value = null
      summary.value = result
      announce(result)
      return true
    } catch (error) {
      await recordImportError(error)
      return false
    } finally {
      importing.value = false
    }
  }

  /** Drops the held file without importing (Cancel import). */
  function cancelRefusal() {
    pendingRefusal.value = null
  }

  // A different vacancy or round starts with a clean import slate, held file included.
  watch([vacancyId, roundId], () => {
    importing.value = false
    importError.value = null
    summary.value = null
    pendingRefusal.value = null
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

  async function recordImportError(error: unknown): Promise<void> {
    const malformed = malformedCsvAlert(error)
    if (malformed) {
      importError.value = malformed
      return
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
  }

  return {
    importing,
    importError,
    summary,
    summaryLine,
    pendingRefusal,
    importFile,
    importWithLayout,
    confirmDrift,
    cancelRefusal,
    clearResult,
  }
}

const layoutRequiredCode = 'Candidates.FormLayoutRequired'
const headerDriftCode = 'Candidates.FormHeaderDrift'

/**
 * Parses the two 409 refusals (problem title + extensions). Malformed
 * extensions return null so the call falls through to the generic path.
 */
function formImportRefusal(error: unknown, file: File): FormImportRefusal | null {
  if (!(error instanceof ApiError) || error.status !== 409) {
    return null
  }
  const problem = error.problem
  if (typeof problem !== 'object' || problem === null) {
    return null
  }
  const { title, headers, changes } = problem as {
    title?: unknown
    headers?: unknown
    changes?: unknown
  }
  if (title !== layoutRequiredCode && title !== headerDriftCode) {
    return null
  }
  if (!Array.isArray(headers) || !headers.every((header) => typeof header === 'string')) {
    return null
  }
  if (title === layoutRequiredCode) {
    return { file, headers, changes: null }
  }
  if (!Array.isArray(changes) || !changes.every(isHeaderChange)) {
    return null
  }
  return { file, headers, changes }
}

function isHeaderChange(value: unknown): value is FormHeaderChangeDto {
  if (typeof value !== 'object' || value === null) {
    return false
  }
  const candidate = value as { ordinal?: unknown; was?: unknown; now?: unknown }
  return (
    typeof candidate.ordinal === 'number' &&
    typeof candidate.was === 'string' &&
    typeof candidate.now === 'string'
  )
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
