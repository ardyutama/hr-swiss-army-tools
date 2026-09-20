import { computed, shallowRef, watch, type Ref } from 'vue'
import { useToast } from '@nuxt/ui/composables/useToast'
import type { CandidateSummary } from '@/features/candidates/api'
import { fieldErrorsOf, firstNonFieldError } from '@/shared/validation'
import { useActionDialog } from '@/shared/useActionDialog'
import type { VacancyRound } from '@/features/vacancies/api'
import { roundDisplayName } from '@/features/intake-rounds/useIntakeRounds'
import { problemMessage, problemMessageText } from '@/shared/problem-details'
import { getPromoteSummary, promoteCandidates } from './api'

export function usePromoteCandidates(
  vacancyId: Ref<string>,
  activeRound: Ref<VacancyRound | null>,
  closedRounds: Ref<VacancyRound[]>,
  enabled: Ref<boolean>,
  onChanged: () => Promise<void>,
) {
  const toast = useToast()
  const sourceRoundId = shallowRef<number | null>(null)
  const sourceCandidates = shallowRef<CandidateSummary[] | null>(null)
  const sourceLoading = shallowRef(false)
  const sourceError = shallowRef<string | null>(null)
  const selectedCandidateIds = shallowRef<number[]>([])
  const submitting = shallowRef(false)
  const {
    open,
    error: submitError,
    request,
    confirm,
  } = useActionDialog({
    onReset() {
      sourceCandidates.value = null
      sourceError.value = null
      selectedCandidateIds.value = []
    },
  })
  let sourceRequestToken = 0

  const promotableCandidates = computed(() =>
    (sourceCandidates.value ?? []).filter(
      (candidate) =>
        candidate.hireOutcome === 'none' &&
        candidate.reviewStatus !== 'rejected',
    ),
  )
  const canSubmit = computed(
    () =>
      enabled.value &&
      activeRound.value !== null &&
      sourceRoundId.value !== null &&
      selectedCandidateIds.value.length > 0 &&
      !sourceLoading.value &&
      !submitting.value,
  )

  function latestClosedRound(): VacancyRound | null {
    return closedRounds.value[0] ?? null
  }

  function resetDialog() {
    sourceRoundId.value = latestClosedRound()?.id ?? null
    sourceCandidates.value = null
    sourceError.value = null
    selectedCandidateIds.value = []
  }

  async function loadSourceCandidates(roundId: number) {
    const token = ++sourceRequestToken
    sourceLoading.value = true
    sourceError.value = null
    sourceCandidates.value = null
    selectedCandidateIds.value = []
    try {
      // The promote summary is unpaged and pre-filtered to eligible
      // candidates; the promotable computed keeps its client-side guard.
      const candidates = await getPromoteSummary(vacancyId.value, roundId)
      if (token !== sourceRequestToken) {
        return
      }
      sourceCandidates.value = candidates
    } catch (error) {
      if (token !== sourceRequestToken) {
        return
      }
      const sourceRound = closedRounds.value.find((round) => round.id === roundId)
      const message = problemMessage(error, 'Something went wrong', {
        round: sourceRound ? roundDisplayName(sourceRound) : undefined,
      })
      if (message.kind !== 'failure') {
        await onChanged()
      }
      sourceError.value = problemMessageText(message)
    } finally {
      if (token === sourceRequestToken) {
        sourceLoading.value = false
      }
    }
  }

  watch([open, sourceRoundId], ([isOpen, roundId]) => {
    if (isOpen && roundId !== null) {
      void loadSourceCandidates(roundId)
    }
  })

  function openDialog() {
    if (!enabled.value) {
      return
    }
    resetDialog()
    request()
  }

  async function submit(): Promise<boolean> {
    const targetRound = activeRound.value
    const sourceId = sourceRoundId.value
    if (!canSubmit.value || targetRound === null || sourceId === null) {
      return false
    }

    submitting.value = true
    try {
      const result = await confirm(async () => {
        try {
          const moved = await promoteCandidates(vacancyId.value, targetRound.id, {
            sourceRoundId: sourceId,
            candidateIds: selectedCandidateIds.value,
          })
          await onChanged()
          const label = moved.length === 1 ? 'candidate' : 'candidates'
          toast.add({ title: `${moved.length} ${label} promoted successfully`, color: 'success' })
          return true
        } catch (error) {
          const fieldErrors = fieldErrorsOf(error)
          if (fieldErrors) {
            return firstNonFieldError(fieldErrors, new Set()) ?? 'Something went wrong'
          }
          const sourceRound = closedRounds.value.find((round) => round.id === sourceId)
          const message = problemMessage(error, 'Something went wrong', {
            round: sourceRound ? roundDisplayName(sourceRound) : undefined,
          })
          if (message.kind !== 'failure') {
            await onChanged()
          }
          return problemMessageText(message)
        }
      })
      return result === true
    } finally {
      submitting.value = false
    }
  }

  return {
    open,
    sourceRoundId,
    sourceCandidates,
    sourceLoading,
    sourceError,
    promotableCandidates,
    selectedCandidateIds,
    submitting,
    submitError,
    canSubmit,
    openDialog,
    submit,
  }
}