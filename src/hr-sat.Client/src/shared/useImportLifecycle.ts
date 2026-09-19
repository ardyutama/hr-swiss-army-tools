import { shallowRef, watch, type Ref } from 'vue'
import { problemMessage, type ProblemMessage } from './problem-details'
import { fieldErrorsOf, type FieldErrors } from './validation'

/**
 * How an attempt's own error routing resolved an error ahead of the shared
 * taxonomy: `unhandled` falls through to `problemMessage`; `handled` stops
 * silently (a refusal routed into the adapter's own step, nothing to show);
 * `alert` surfaces the caller-mapped alert verbatim — an input refusal, so
 * nothing changed server-side and no refresh is announced.
 */
export type ImportInterception<TAlert> =
  | { kind: 'unhandled' }
  | { kind: 'handled' }
  | { kind: 'alert'; alert: TAlert }

export interface ImportLifecycleOptions<TChange extends string, TAlert> {
  vacancyId: Ref<string>
  roundId: Ref<string>
  /** Fallback title for failures the taxonomy does not recognize. */
  fallback: string
  /** Shapes a taxonomy message into the adapter's alert — never re-decides policy. */
  mapMessage: (message: ProblemMessage) => TAlert
  /**
   * Shapes a field-error payload (the server refusing the request's input)
   * into the adapter's alert. Omit when the endpoint has no field-error
   * contract of its own; the payload then falls through to the taxonomy.
   */
  mapFieldErrors?: (errors: FieldErrors) => TAlert
  /** Announces every terminal outcome that changed server state. */
  onChanged?: (changed: TChange) => Promise<void>
}

export interface ImportAttempt<TChange extends string, TResult, TAlert> {
  /** What a successful attempt changed server-side; reported to `onChanged`. */
  change: TChange
  operation: () => Promise<TResult>
  /** Terminal success: record the outcome and announce it; runs before `onChanged`. */
  announce: (result: TResult) => void
  /** Routes errors the adapter recognizes (refusals, malformed payloads) ahead of the taxonomy. */
  intercept?: (error: unknown) => ImportInterception<TAlert>
  /** What a non-failure error may have changed; defaults to `change`. */
  errorChange?: TChange
  /** Busy flag when the attempt runs on its own (e.g. a refusal's submit), not the lifecycle's. */
  busy?: Ref<boolean>
}

/**
 * The import lifecycle skeleton every Intake Source channel shares: guard
 * against re-entry, clear the last alert, run the operation, announce
 * success, and report the change — failures route through the adapter's
 * interceptions first, then the shared taxonomy (`fieldErrorsOf` →
 * `problemMessage` → the caller's alert mapper), announcing a refresh
 * whenever the outcome may have changed server state. Channel-specifics —
 * multi-file results, Form Response refusals — stay in the adapters; the
 * core is deliberately channel-agnostic.
 */
export function useImportLifecycle<TChange extends string, TAlert>(
  options: ImportLifecycleOptions<TChange, TAlert>,
) {
  const busy = shallowRef(false)
  const alert = shallowRef<TAlert | null>(null)

  async function attempt<TResult>(
    attempt: ImportAttempt<TChange, TResult, TAlert>,
  ): Promise<TResult | null> {
    const flag = attempt.busy ?? busy
    if (flag.value) {
      return null
    }
    flag.value = true
    alert.value = null
    try {
      const result = await attempt.operation()
      attempt.announce(result)
      await options.onChanged?.(attempt.change)
      return result
    } catch (error) {
      const interception = attempt.intercept?.(error)
      if (interception?.kind === 'handled') {
        return null
      }
      if (interception?.kind === 'alert') {
        alert.value = interception.alert
        return null
      }
      await recordError(error, attempt.errorChange ?? attempt.change)
      return null
    } finally {
      flag.value = false
    }
  }

  /** The shared error taxonomy: field-error payloads verbatim, everything else via `problemMessage`. */
  async function recordError(error: unknown, change: TChange): Promise<void> {
    const fieldErrors = fieldErrorsOf(error)
    if (fieldErrors && options.mapFieldErrors) {
      alert.value = options.mapFieldErrors(fieldErrors)
      return
    }
    const message = problemMessage(error, options.fallback)
    if (message.kind !== 'failure') {
      await options.onChanged?.(change)
    }
    alert.value = options.mapMessage(message)
  }

  // A different vacancy or round starts with a clean import slate.
  watch([options.vacancyId, options.roundId], () => {
    busy.value = false
    alert.value = null
  })

  /** Dismisses a visible alert (e.g. when the import dialog closes). */
  function clearAlert() {
    alert.value = null
  }

  return { busy, alert, attempt, clearAlert }
}
