import { computed, shallowRef, watch, type Ref } from 'vue'
import type { VacancyDetails, VacancyRound } from '@/features/vacancies/api'

/**
 * The leaf lifecycles the composer coordinates, supplied by the view: the
 * vacancy and its rounds (round state derived by `useIntakeRounds`), plus the
 * reloads behind the two refresh announcements. The reloads are invoked only
 * from mutations and import outcomes, never during setup.
 */
export interface VacancyDetailFlowInput {
  vacancy: Ref<VacancyDetails | null>
  rounds: Ref<VacancyRound[]>
  activeRound: Ref<VacancyRound | null>
  closedRounds: Ref<VacancyRound[]>
  reloadVacancy: () => Promise<void>
  reloadCandidates: () => Promise<void>
  reloadLayout: () => Promise<void>
  // The messaging summary rides the mutation cascades: a reclassify, import,
  // delete, or promote changes who the send flow and template preview see.
  reloadMessaging: () => Promise<void>
}

/**
 * Coordination-only composer for the vacancy-detail page. It owns what the
 * page's flows share and nothing else: the selected Intake Round (one owner
 * for its four-plus consumers), the cross-feature lifecycle flags derived
 * from it, and the two refresh announcements the import adapters and the
 * mutations invoke. Leaf lifecycles — vacancy, candidates, layout, rounds,
 * messaging — are composed by the view itself; a composer that re-exported
 * them unchanged would be a pass-through facade.
 */
export function useVacancyDetailFlow(input: VacancyDetailFlowInput) {
  const { vacancy, rounds, activeRound, closedRounds } = input

  const selectedRoundId = shallowRef<number | null>(null)
  const selectedRound = computed(
    () => rounds.value.find((round) => round.id === selectedRoundId.value) ?? null,
  )

  // Round Management re-pins to the active round (else the latest) when the
  // selected round disappears from a refreshed rollup.
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

  const isClosed = computed(() => vacancy.value?.status === 'closed')
  const selectedRoundClosed = computed(() => selectedRound.value?.status === 'closed')
  const candidatesReadonly = computed(() => isClosed.value || selectedRoundClosed.value)
  const canImport = computed(() => !candidatesReadonly.value && activeRound.value !== null)

  const canPromote = computed(
    () =>
      !isClosed.value &&
      selectedRound.value?.status === 'open' &&
      activeRound.value?.id === selectedRound.value?.id &&
      closedRounds.value.length > 0,
  )

  function selectRound(round: VacancyRound) {
    selectedRoundId.value = round.id
  }

  const refreshVacancyAndCandidates = async () => {
    await Promise.all([input.reloadVacancy(), input.reloadCandidates(), input.reloadMessaging()])
  }
  const refreshVacancyCandidatesAndLayout = async () => {
    await Promise.all([
      input.reloadVacancy(),
      input.reloadCandidates(),
      input.reloadLayout(),
      input.reloadMessaging(),
    ])
  }

  return {
    selectedRoundId,
    selectedRound,
    selectRound,
    isClosed,
    candidatesReadonly,
    canImport,
    canPromote,
    refreshVacancyAndCandidates,
    refreshVacancyCandidatesAndLayout,
  }
}
