import { computed, shallowRef, watch, type Ref } from 'vue'
import { getVacancy, type VacancyDetails } from '@/features/vacancies/api'
import {
  listCandidates,
  type CandidateReviewStatus,
  type CandidateSummary,
} from '@/features/candidates/api'
import {
  getCandidateDetails,
  updateCandidateDetails,
  updateCandidateNotes,
  updateCandidateRequirementReview,
  updateCandidateReview,
  type CandidateDetailsPayload,
  type CandidateDetails,
} from './api'
import { notesValidationError } from './validation'

export type ReviewViewState = 'loading' | 'error' | 'ready'
export type NotesSaveState = 'idle' | 'saving' | 'saved' | 'error'

/**
 * Owns the S4 review-workspace lifecycle: vacancy context (title, ordered
 * requirements), the candidate ordering for Prev/Next, the current candidate's
 * details, and the notes auto-save contract from ADR-0008 #9 (decision and
 * navigation silently commit pending notes — no explicit Save).
 */
export function useReview(vacancyId: Ref<string>, candidateId: Ref<string>) {
  const vacancy = shallowRef<VacancyDetails | null>(null)
  const summaries = shallowRef<CandidateSummary[] | null>(null)
  const candidate = shallowRef<CandidateDetails | null>(null)
  const loadError = shallowRef<string | null>(null)

  const notes = shallowRef('')
  const notesSaveState = shallowRef<NotesSaveState>('idle')
  const notesSavedAt = shallowRef<Date | null>(null)

  const savingDetails = shallowRef(false)
  const detailsError = shallowRef<string | null>(null)
  const savingRequirementId = shallowRef<number | null>(null)
  const requirementError = shallowRef<string | null>(null)
  const deciding = shallowRef(false)
  const decisionError = shallowRef<string | null>(null)

  let contextToken = 0
  let detailsToken = 0

  const currentIndex = computed(
    () => summaries.value?.findIndex((summary) => String(summary.id) === candidateId.value) ?? -1,
  )
  const total = computed(() => summaries.value?.length ?? 0)
  /** 1-based position of the current candidate; 0 while the ordering is unknown. */
  const position = computed(() => (currentIndex.value < 0 ? 0 : currentIndex.value + 1))
  const previousCandidateId = computed(() =>
    currentIndex.value > 0 ? String(summaries.value![currentIndex.value - 1]!.id) : null,
  )
  const nextCandidateId = computed(() =>
    currentIndex.value >= 0 && currentIndex.value < total.value - 1
      ? String(summaries.value![currentIndex.value + 1]!.id)
      : null,
  )

  const notesDirty = computed(
    () => candidate.value !== null && notes.value !== (candidate.value.notes ?? ''),
  )

  const viewState = computed<ReviewViewState>(() => {
    if (loadError.value !== null) {
      return 'error'
    }
    if (vacancy.value === null || candidate.value === null) {
      return 'loading'
    }
    return 'ready'
  })

  function patchSummary(updated: CandidateDetails) {
    if (summaries.value === null) {
      return
    }

    summaries.value = summaries.value.map((summary) =>
      summary.id === updated.id
        ? {
            ...summary,
            fullName: updated.fullName,
            contactEmail: updated.contactEmail,
            notes: updated.notes,
            reviewStatus: updated.reviewStatus,
          }
        : summary,
    )

    if (vacancy.value !== null) {
      const processedCandidates = summaries.value.filter(
        (summary) =>
          summary.reviewStatus === 'shortlisted' || summary.reviewStatus === 'rejected',
      ).length
      vacancy.value = {
        ...vacancy.value,
        progress: {
          processedCandidates,
          totalCandidates: summaries.value.length,
        },
      }
    }
  }

  async function loadContext() {
    const token = ++contextToken
    loadError.value = null
    try {
      const [details, list] = await Promise.all([
        getVacancy(vacancyId.value),
        listCandidates(vacancyId.value),
      ])
      // Ignore stale responses when the route param changed meanwhile.
      if (token !== contextToken) {
        return
      }
      vacancy.value = details
      summaries.value = list
    } catch (error) {
      if (token !== contextToken) {
        return
      }
      loadError.value = error instanceof Error ? error.message : 'Failed to load the vacancy'
    }
  }

  async function loadDetails() {
    const token = ++detailsToken
    loadError.value = null
    const id = Number(candidateId.value)
    if (!Number.isFinite(id)) {
      loadError.value = 'Unknown candidate'
      return
    }
    try {
      const details = await getCandidateDetails(vacancyId.value, id)
      if (token !== detailsToken) {
        return
      }
      candidate.value = details
      notes.value = details.notes ?? ''
      notesSaveState.value = 'idle'
      notesSavedAt.value = null
      detailsError.value = null
      requirementError.value = null
    } catch (error) {
      if (token !== detailsToken) {
        return
      }
      loadError.value = error instanceof Error ? error.message : 'Failed to load the candidate'
    }
  }

  async function updateDetails(payload: CandidateDetailsPayload): Promise<boolean> {
    const current = candidate.value
    if (!current || savingDetails.value) {
      return false
    }

    savingDetails.value = true
    detailsError.value = null
    try {
      const updated = await updateCandidateDetails(vacancyId.value, current.id, payload)
      if (candidate.value?.id !== updated.id) {
        return false
      }
      candidate.value = updated
      patchSummary(updated)
      return true
    } catch (error) {
      detailsError.value = error instanceof Error ? error.message : 'Failed to save candidate details'
      return false
    } finally {
      savingDetails.value = false
    }
  }

  async function updateRequirementReview(
    requirementId: number,
    confirmed: boolean,
  ): Promise<boolean> {
    const current = candidate.value
    if (!current || savingRequirementId.value !== null) {
      return false
    }

    savingRequirementId.value = requirementId
    requirementError.value = null
    try {
      const updated = await updateCandidateRequirementReview(
        vacancyId.value,
        current.id,
        requirementId,
        confirmed,
      )
      if (candidate.value?.id !== updated.id) {
        return false
      }
      candidate.value = updated
      return true
    } catch (error) {
      requirementError.value =
        error instanceof Error ? error.message : 'Failed to save the requirement review'
      return false
    } finally {
      savingRequirementId.value = null
    }
  }

  /** Commits pending notes. Returns false when the save failed or was invalid. */
  async function saveNotes(): Promise<boolean> {
    const current = candidate.value
    if (!current || !notesDirty.value) {
      return true
    }
    if (notesValidationError(notes.value) !== null) {
      notesSaveState.value = 'error'
      return false
    }
    notesSaveState.value = 'saving'
    try {
      const updated = await updateCandidateNotes(vacancyId.value, current.id, notes.value)
      if (candidate.value?.id !== updated.id) {
        return false
      }
      candidate.value = updated
      notes.value = updated.notes ?? ''
      notesSaveState.value = 'saved'
      notesSavedAt.value = new Date()
      patchSummary(updated)
      return true
    } catch {
      notesSaveState.value = 'error'
      return false
    }
  }

  /**
   * Decision-as-commit (ADR-0008 #9): saves pending notes and sets the review
   * status in one call. Returns the next candidate id to auto-advance to, or
   * null when there is nowhere to go (last candidate) or the decision failed.
   */
  async function decide(status: CandidateReviewStatus): Promise<string | null> {
    const current = candidate.value
    if (!current || deciding.value) {
      return null
    }
    if (notesValidationError(notes.value) !== null) {
      notesSaveState.value = 'error'
      return null
    }
    deciding.value = true
    decisionError.value = null
    try {
      const updated = await updateCandidateReview(vacancyId.value, current.id, {
        reviewStatus: status,
        notes: notes.value,
      })
      if (candidate.value?.id !== updated.id) {
        return null
      }
      candidate.value = updated
      notes.value = updated.notes ?? ''
      notesSaveState.value = 'saved'
      notesSavedAt.value = new Date()
      patchSummary(updated)
      return nextCandidateId.value
    } catch (error) {
      decisionError.value =
        error instanceof Error ? error.message : 'Failed to save the review decision'
      return null
    } finally {
      deciding.value = false
    }
  }

  watch(notes, () => {
    if (notesDirty.value && notesSaveState.value !== 'saving') {
      notesSaveState.value = 'idle'
    }
  })

  // Reload the vacancy context when the vacancy route param changes.
  watch(
    vacancyId,
    () => {
      vacancy.value = null
      summaries.value = null
      candidate.value = null
      loadError.value = null
      void loadContext()
    },
    { immediate: true },
  )

  // Reload the candidate when the route param changes without leaving the view.
  watch(
    candidateId,
    () => {
      candidate.value = null
      notes.value = ''
      savingDetails.value = false
      detailsError.value = null
      savingRequirementId.value = null
      requirementError.value = null
      decisionError.value = null
      void loadDetails()
    },
    { immediate: true },
  )

  return {
    vacancy,
    candidate,
    loadError,
    viewState,
    position,
    total,
    previousCandidateId,
    nextCandidateId,
    notes,
    notesDirty,
    notesSaveState,
    notesSavedAt,
    savingDetails,
    detailsError,
    savingRequirementId,
    requirementError,
    deciding,
    decisionError,
    load: async () => {
      await loadContext()
      await loadDetails()
    },
    saveNotes,
    updateDetails,
    updateRequirementReview,
    decide,
  }
}
