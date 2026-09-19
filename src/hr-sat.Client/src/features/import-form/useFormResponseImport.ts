import { computed, shallowRef, watch, type Ref } from 'vue'
import { useToast } from '@nuxt/ui/composables/useToast'
import { ApiError } from '@/shared/http'
import { fieldErrorsOf, firstNonFieldError } from '@/shared/validation'
import { type ProblemMessageColor } from '@/shared/problem-details'
import { useImportLifecycle, type ImportInterception } from '@/shared/useImportLifecycle'
import type { FormLayoutColumn, FormLayoutDto } from '@/features/form-layout/api'
import { describeHeaderChanges, type FormHeaderChangeDto, type FormHeaderChangeView } from './format'
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

/** What a terminal outcome changed server-side: layout-stamping paths also change the Form Layout. */
export type FormImportChange = 'candidates' | 'candidatesAndLayout'

/**
 * The Form Response import lifecycle as one union, never parallel booleans.
 * `guidedSetup` means the vacancy has no valid Form Layout yet; `headerDrift`
 * carries the enriched changes (role + Column Label + missing flag), ready to
 * bind. The held file's single owner is here.
 */
export type FormImportStep =
  | { kind: 'idle' }
  | { kind: 'uploading' }
  | { kind: 'guidedSetup'; file: File; headers: string[]; submitting: boolean }
  | {
      kind: 'headerDrift'
      file: File
      headers: string[]
      changes: FormHeaderChangeView[]
      submitting: boolean
    }

/**
 * One deep module for the whole Form Response import: upload, the two
 * refusals (guided setup, Header Drift), re-map, confirm, and cancel. The
 * layout arrives by injection — this module never loads it — and every
 * terminal outcome that changed server state invokes `onChanged` so the
 * caller decides what to reload.
 */
export function useFormResponseImport(
  vacancyId: Ref<string>,
  roundId: Ref<string>,
  layout: Ref<FormLayoutDto | null>,
  onChanged?: (changed: FormImportChange) => Promise<void>,
) {
  const toast = useToast()
  const lifecycle = useImportLifecycle<FormImportChange, FormImportAlert>({
    vacancyId,
    roundId,
    fallback: "Couldn't import the form responses",
    mapMessage: (message) => ({
      color: message.color,
      title: message.title,
      description: message.description,
    }),
    onChanged,
  })
  // The lifecycle's busy flag is the upload; refusal submissions run on their own.
  const uploading = lifecycle.busy
  const alert = lifecycle.alert
  const submitting = shallowRef(false)
  const summary = shallowRef<FormImportSummary | null>(null)
  const pendingRefusal = shallowRef<FormImportRefusal | null>(null)

  const summaryLine = computed(() =>
    summary.value ? formImportSummaryLine(summary.value) : null,
  )

  // Derived over the raw refusal: drift enrichment reacts if the layout
  // arrives after the refusal, and terminal success returns the step to idle.
  const step = computed<FormImportStep>(() => {
    const refusal = pendingRefusal.value
    if (refusal === null) {
      return uploading.value ? { kind: 'uploading' } : { kind: 'idle' }
    }
    if (refusal.changes === null) {
      return {
        kind: 'guidedSetup',
        file: refusal.file,
        headers: refusal.headers,
        submitting: submitting.value,
      }
    }
    return {
      kind: 'headerDrift',
      file: refusal.file,
      headers: refusal.headers,
      changes: describeHeaderChanges(refusal.changes, layout.value?.columns ?? []),
      submitting: submitting.value,
    }
  })

  // The dialog bindings are derived here, not in the view: they are refusals
  // of the step union, and deriving them elsewhere would re-implement the
  // union's semantics. `guidedSetup` / `headerDrift` are the narrowed refusal
  // steps (null when absent); `idle` is the absence of any upload or refusal.
  const guidedSetup = computed(() => (step.value.kind === 'guidedSetup' ? step.value : null))
  const headerDrift = computed(() => (step.value.kind === 'headerDrift' ? step.value : null))
  const idle = computed(() => step.value.kind === 'idle')

  // The layout panel maps the held file's headers in guided mode and the
  // saved snapshot in edit mode; the layout arrives by injection either way.
  const panelHeaders = computed(
    () => guidedSetup.value?.headers ?? layout.value?.headerSnapshot ?? [],
  )

  /**
   * The refusal routes ahead of the shared taxonomy: the two 409 codes hold
   * the file and swap the step (importFile only — a layout-payload retry is
   * never refused again); a 400 is always a malformed CSV (ADR-0013) and is
   * surfaced verbatim with no refresh.
   */
  function intercept(error: unknown, file: File | null): ImportInterception<FormImportAlert> {
    if (file !== null) {
      const refusal = formImportRefusal(error, file)
      if (refusal) {
        pendingRefusal.value = refusal
        return { kind: 'handled' }
      }
    }
    const malformed = malformedCsvAlert(error)
    return malformed ? { kind: 'alert', alert: malformed } : { kind: 'unhandled' }
  }

  async function importFile(file: File): Promise<void> {
    if (step.value.kind !== 'idle') {
      return
    }
    summary.value = null
    await lifecycle.attempt({
      change: 'candidates',
      operation: () => importFormResponses(vacancyId.value, roundId.value, file),
      intercept: (error) => intercept(error, file),
      announce: (result) => {
        summary.value = result
        announce(result)
      },
    })
  }

  /** Guided setup / drift re-map: re-send the held file with the mapping as the layout payload. */
  async function submitLayout(columns: FormLayoutColumn[]): Promise<void> {
    const refusal = pendingRefusal.value
    if (submitting.value || refusal === null) {
      return
    }
    await lifecycle.attempt({
      busy: submitting,
      change: 'candidatesAndLayout',
      // A failed submission still only touched candidates server-side.
      errorChange: 'candidates',
      operation: () =>
        importFormResponsesWithLayout(vacancyId.value, roundId.value, refusal.file, columns),
      // The refusal stays pending on error so the dialog remains open with its mapping.
      intercept: (error) => intercept(error, null),
      announce: (result) => {
        pendingRefusal.value = null
        summary.value = result
        announce(result)
      },
    })
  }

  /** Drift confirm: re-send the held file with the confirmDrift flag. */
  async function confirmDrift(): Promise<void> {
    const refusal = pendingRefusal.value
    if (submitting.value || refusal === null) {
      return
    }
    await lifecycle.attempt({
      busy: submitting,
      change: 'candidatesAndLayout',
      errorChange: 'candidates',
      operation: () => confirmFormImportDrift(vacancyId.value, roundId.value, refusal.file),
      // The refusal stays pending on error so the drift dialog stays open on its mapping.
      intercept: (error) => intercept(error, null),
      announce: (result) => {
        pendingRefusal.value = null
        summary.value = result
        announce(result)
      },
    })
  }

  /** Drift → guided transition: the refusal stays held; the panel re-maps it. */
  function remap() {
    const refusal = pendingRefusal.value
    if (refusal?.changes) {
      pendingRefusal.value = { ...refusal, changes: null }
    }
  }

  /** Drops the held file without importing (Cancel import). */
  function cancel() {
    pendingRefusal.value = null
  }

  // A different vacancy or round starts with a clean import slate, held file
  // included (the lifecycle resets its own busy flag and alert).
  watch([vacancyId, roundId], () => {
    submitting.value = false
    summary.value = null
    pendingRefusal.value = null
  })

  /** Dismisses a visible summary or import error (e.g. when the import dialog closes). */
  function clearResult() {
    lifecycle.clearAlert()
    summary.value = null
  }

  function announce(result: FormImportSummary): void {
    toast.add({
      title: `Form import complete: ${formImportSummaryLine(result)}`,
      color: result.created + result.updated > 0 ? 'success' : 'neutral',
    })
  }

  return {
    step,
    guidedSetup,
    headerDrift,
    idle,
    uploading,
    panelHeaders,
    alert,
    summary,
    summaryLine,
    importFile,
    submitLayout,
    confirmDrift,
    remap,
    cancel,
    clearResult,
  }
}

const layoutRequiredCode = 'Candidates.FormLayoutRequired'
const headerDriftCode = 'Candidates.FormHeaderDrift'

/**
 * A refused form import holding its file: `changes` null means the vacancy has
 * no valid Form Layout yet (guided setup); a list means Header Drift.
 */
interface FormImportRefusal {
  file: File
  headers: string[]
  changes: FormHeaderChangeDto[] | null
}

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
