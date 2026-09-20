import { computed, shallowRef, watch, type Ref } from 'vue'
import { ApiError } from '@/shared/http'
import { problemMessage, problemMessageText } from '@/shared/problem-details'
import { getFormLayout, type FormLayoutDto } from '@/features/form-layout/api'
import type { CandidateDetails } from './api'
import {
  currentFormResponse,
  formAnswerRows,
  formCvLink,
  formSubmittedAt,
  type FormAnswerRow,
} from './format'

export type FormAnswersViewState = 'idle' | 'loading' | 'error' | 'ready'

/**
 * Owns the review page's form-evidence lifecycle: the vacancy's Form Layout is
 * fetched lazily — only once a form-sourced candidate is on screen — and
 * projected over the current Form Response into answer rows, the CV link, and
 * the header's submitted line. A 404 means the vacancy has no layout, which the
 * panel renders as its empty state rather than an error.
 */
export function useFormAnswers(
  vacancyId: Ref<string>,
  candidate: Ref<CandidateDetails | null>,
) {
  const layout = shallowRef<FormLayoutDto | null>(null)
  const layoutMissing = shallowRef(false)
  const loadError = shallowRef<string | null>(null)
  const requested = shallowRef(false)
  let requestToken = 0

  const isFormCandidate = computed(() => candidate.value?.intakeSource === 'form')
  const response = computed(() =>
    candidate.value === null ? null : currentFormResponse(candidate.value),
  )

  const viewState = computed<FormAnswersViewState>(() => {
    if (!isFormCandidate.value) {
      return 'idle'
    }
    if (loadError.value !== null) {
      return 'error'
    }
    if (layout.value === null && !layoutMissing.value) {
      return 'loading'
    }
    return 'ready'
  })

  const answers = computed<FormAnswerRow[]>(() =>
    viewState.value === 'ready' ? formAnswerRows(layout.value, response.value) : [],
  )
  // formCvLink tolerates a null layout, so the link simply stays null while loading.
  const cvLink = computed(() => formCvLink(layout.value, response.value))
  const submittedAt = computed(() =>
    isFormCandidate.value ? formSubmittedAt(response.value) : null,
  )

  async function load() {
    const token = ++requestToken
    requested.value = true
    loadError.value = null
    layoutMissing.value = false
    try {
      const dto = await getFormLayout(vacancyId.value)
      // Ignore stale responses when the vacancy changed meanwhile.
      if (token !== requestToken) {
        return
      }
      layout.value = dto
    } catch (error) {
      if (token !== requestToken) {
        return
      }
      if (error instanceof ApiError && error.status === 404) {
        layout.value = null
        layoutMissing.value = true
        return
      }
      loadError.value = problemMessageText(problemMessage(error, "Couldn't load the form answers"))
    }
  }

  function retry() {
    void load()
  }

  // The layout is vacancy-level evidence configuration: fetch it once the first
  // form candidate appears, never for email candidates.
  watch(isFormCandidate, (isForm) => {
    if (isForm && !requested.value) {
      void load()
    }
  })

  // A different vacancy restarts the lifecycle; the next form candidate refetches.
  // Declared after the candidate watcher — order is safe anyway: useReview nulls
  // the candidate on a vacancy switch, so isFormCandidate flips false → true with
  // the new candidate and re-arms the fetch against the reset `requested` flag.
  watch(vacancyId, () => {
    requestToken++
    layout.value = null
    layoutMissing.value = false
    loadError.value = null
    requested.value = false
  })

  return {
    formAnswersState: viewState,
    formAnswersError: loadError,
    formAnswers: answers,
    cvLink,
    submittedAt,
    retryFormAnswers: retry,
  }
}
