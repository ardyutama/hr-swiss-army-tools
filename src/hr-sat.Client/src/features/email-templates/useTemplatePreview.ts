import { onScopeDispose, shallowRef, toValue, type MaybeRefOrGetter } from 'vue'
import type { CandidateSummary } from '@/features/candidates/api'
import { problemMessage, problemMessageText } from '@/shared/problem-details'
import { renderEmailTemplate, type EmailTemplateKind, type RenderedMessage } from './api'

export interface TemplateRenderRequest {
  subject: string
  body: string
  candidateId: number
}

const PREVIEW_DEBOUNCE_MS = 350

/**
 * Live Prepared-Message preview against `POST /render`. The initial render for
 * a dialog fires immediately; subsequent subject/body/candidate changes are
 * debounced and the latest call wins (precedent: `beginEdit` in useVacancies),
 * so typing never hammers the endpoint and a stale response never overwrites a
 * newer draft. Identical consecutive requests are skipped.
 */
export function useTemplatePreview(vacancyId: MaybeRefOrGetter<string>) {
  const preview = shallowRef<RenderedMessage | null>(null)
  const previewing = shallowRef(false)
  const previewError = shallowRef<string | null>(null)
  let token = 0
  let timer: ReturnType<typeof setTimeout> | null = null
  let lastRequestKey: string | null = null

  function cancelTimer() {
    if (timer !== null) {
      clearTimeout(timer)
      timer = null
    }
  }

  function requestKey(payload: TemplateRenderRequest): string {
    return [payload.candidateId, payload.subject, payload.body].join('')
  }

  async function run(requestToken: number, payload: TemplateRenderRequest) {
    previewing.value = true
    try {
      const rendered = await renderEmailTemplate(toValue(vacancyId), payload)
      if (requestToken === token) {
        preview.value = rendered
        previewError.value = null
      }
    } catch (error) {
      if (requestToken === token) {
        preview.value = null
        previewError.value = problemMessageText(
          problemMessage(error, "Couldn't preview the prepared message"),
        )
      }
    } finally {
      if (requestToken === token) {
        previewing.value = false
      }
    }
  }

  /** Renders now — dialog open and other discrete moments. */
  function renderNow(payload: TemplateRenderRequest) {
    const key = requestKey(payload)
    if (key === lastRequestKey) {
      return
    }
    lastRequestKey = key
    cancelTimer()
    void run(++token, payload)
  }

  /** Renders after a quiet period — typing. */
  function schedule(payload: TemplateRenderRequest) {
    const key = requestKey(payload)
    if (key === lastRequestKey) {
      return
    }
    lastRequestKey = key
    cancelTimer()
    const requestToken = ++token
    timer = setTimeout(() => {
      timer = null
      void run(requestToken, payload)
    }, PREVIEW_DEBOUNCE_MS)
  }

  function clear() {
    cancelTimer()
    token++
    lastRequestKey = null
    preview.value = null
    previewError.value = null
    previewing.value = false
  }

  onScopeDispose(cancelTimer)

  return { preview, previewing, previewError, renderNow, schedule, clear }
}

/**
 * Default preview candidate: the first candidate in the viewed round whose
 * review status matches the template kind, else the round's first candidate.
 */
export function defaultPreviewCandidate(
  candidates: readonly CandidateSummary[],
  kind: EmailTemplateKind,
): CandidateSummary | null {
  return candidates.find((candidate) => candidate.reviewStatus === kind) ?? candidates[0] ?? null
}
