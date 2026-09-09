import { computed, shallowRef, watch, type Ref } from 'vue'
import type { CandidateSummary } from '@/features/candidates/api'
import type { CandidateFilterState } from '@/features/candidates/filter'
import { useCandidateFilter } from '@/features/candidates/useCandidateFilter'
import { useCandidateImport } from '@/features/candidates/useCandidateImport'
import { useCandidates } from '@/features/candidates/useCandidates'
import { useIntakeRounds } from '@/features/intake-rounds/useIntakeRounds'
import type { VacancyRound } from '@/features/vacancies/api'
import { progressPercent } from '@/features/vacancies/format'
import { hiringShortage } from '@/features/vacancies/hiring'
import { useVacancyDetail } from './useVacancyDetail'

/**
 * The vacancy-detail flow behind one interface: the vacancy rollup, round
 * selection and lifecycle, the candidate list, .eml import, and the
 * permissibility rules that decide what HR may do from this page (a Closed
 * Vacancy or a closed Intake Round freezes candidate edits; a new round can
 * only open while none is active and the vacancy is open).
 *
 * The route view stays a composition surface: it connects this flow to the
 * router and renders the returned state. Refresh coordination lives here too —
 * imports and deletions change both the vacancy rollup (progress, round counts)
 * and the candidate list, so both reload together.
 */
export function useVacancyDetailFlow(
  vacancyId: Ref<string>,
  initialCandidateFilters: Partial<CandidateFilterState> = {},
) {
  const { vacancy, loadError, viewState, load } = useVacancyDetail(vacancyId)

  const rounds = computed<VacancyRound[]>(() => vacancy.value?.rounds ?? [])

  const {
    creating: creatingRound,
    closing: closingRound,
    activeRound,
    canCreateRound,
    create: createRoundAction,
    close: closeRoundAction,
  } = useIntakeRounds(rounds, vacancyId, load)

  // The selected round drives the candidate list. Default to the active round;
  // a vacancy with no active round shows no candidates (imports are rejected).
  // Round ids are JSON numbers; they are stringified only at the route-param/URL
  // boundary (the round-scoped candidate APIs take route-param-shaped strings).
  const selectedRoundId = shallowRef<number | null>(null)
  const selectedRound = computed(
    () => rounds.value.find((round) => round.id === selectedRoundId.value) ?? null,
  )
  const selectedRoundParam = computed(() =>
    selectedRoundId.value === null ? '' : String(selectedRoundId.value),
  )

  // Keep the selection pinned to the active round whenever the vacancy reloads,
  // unless the user deliberately opened a different (closed) round that still exists.
  watch(
    rounds,
    (current) => {
      if (current.length === 0) {
        selectedRoundId.value = null
        return
      }
      const stillThere = current.some((round) => round.id === selectedRoundId.value)
      if (!stillThere) {
        selectedRoundId.value = (activeRound.value ?? current[current.length - 1])!.id
      }
    },
    { immediate: true },
  )

  // Permissibility rules, derived from the vacancy status and the selected
  // round's lifecycle: what HR may do from this page right now.
  const isClosed = computed(() => vacancy.value?.status === 'closed')
  const selectedRoundClosed = computed(() => selectedRound.value?.status === 'closed')
  const candidatesReadonly = computed(() => isClosed.value || selectedRoundClosed.value)
  const canImport = computed(() => !candidatesReadonly.value && activeRound.value !== null)
  const canOpenRound = computed(() => !isClosed.value && canCreateRound.value)

  const {
    importing,
    importError,
    results,
    importFiles: importCandidateFiles,
    clearError,
  } = useCandidateImport(
    vacancyId,
    selectedRoundParam,
  )
  const {
    candidates,
    loadError: candidatesError,
    viewState: candidatesViewState,
    removing,
    load: loadCandidates,
    remove,
  } = useCandidates(vacancyId, selectedRoundParam)
  const {
    status: statusFilter,
    query: searchQuery,
    receivedSort,
    statusCounts,
    filteredCandidates,
    listState,
    toggleReceivedSort,
    clearFilters,
  } = useCandidateFilter(candidates, {
    viewState: candidatesViewState,
    vacancyClosed: isClosed,
    hasActiveRound: computed(() => activeRound.value !== null),
    selectedRoundClosed,
  }, initialCandidateFilters)

  // The round manager stays flow-owned because it controls round chrome.
  const roundManagementOpen = shallowRef(false)

  const showRoundChrome = computed(() => rounds.value.length > 1 || roundManagementOpen.value)
  const showRoundManager = computed(
    () =>
      !isClosed.value &&
      rounds.value.length === 1 &&
      (activeRound.value !== null || candidatesViewState.value !== 'empty'),
  )

  const progress = computed(() => (vacancy.value ? progressPercent(vacancy.value.progress) : 0))
  const roundShortage = computed(() => {
    const hiring = vacancy.value?.hiring
    return hiring ? hiringShortage(hiring) : null
  })
  const vacancyRequirements = computed(
    () => vacancy.value?.requirements.map((requirement) => requirement.phrase) ?? [],
  )

  async function importFiles(files: File[]): Promise<boolean> {
    const response = await importCandidateFiles(files)
    if (!response) {
      return false
    }
    // Refresh so the vacancy progress, round counts, and the candidate list reflect the import.
    await Promise.all([load(), loadCandidates()])
    return true
  }

  function selectRound(round: VacancyRound) {
    selectedRoundId.value = round.id
  }

  function openRoundManager() {
    roundManagementOpen.value = true
  }

  async function createRound(name: string | null): Promise<boolean> {
    const created = await createRoundAction(name)
    if (!created) {
      return false
    }
    // Opening a round moves the workspace into it.
    selectedRoundId.value = created.id
    return true
  }

  async function closeRound(round: VacancyRound): Promise<boolean> {
    return closeRoundAction(round)
  }

  async function deleteCandidate(candidate: CandidateSummary): Promise<string | null> {
    try {
      await remove(candidate)
      // Progress and round counts include candidates, so refresh the vacancy too.
      await load()
      return null
    } catch (error) {
      return error instanceof Error ? error.message : 'Failed to delete candidate'
    }
  }

  return {
    vacancy: {
      vacancy,
      loadError,
      viewState,
      load,
      progress,
      vacancyRequirements,
    },
    rounds: {
      rounds,
      activeRound,
      creatingRound,
      closingRound,
      canCreateRound,
      selectedRoundId,
      selectRound,
      canOpenRound,
      showRoundChrome,
      showRoundManager,
      roundManagementOpen,
      openRoundManager,
      roundShortage,
      createRound,
      closeRound,
    },
    candidates: {
      candidates,
      candidatesError,
      removing,
      loadCandidates,
      statusFilter,
      searchQuery,
      receivedSort,
      statusCounts,
      filteredCandidates,
      listState,
      toggleReceivedSort,
      clearFilters,
      candidatesReadonly,
      deleteCandidate,
    },
    import: {
      importing,
      importError,
      results,
      clearError,
      canImport,
      importFiles,
    },
  }
}
