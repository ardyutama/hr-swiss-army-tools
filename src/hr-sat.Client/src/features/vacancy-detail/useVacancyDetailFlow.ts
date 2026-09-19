import { computed, shallowRef, watch, type Ref } from 'vue'
import type { CandidateSummary } from '@/features/candidates/api'
import type { CandidateFilterState } from '@/features/candidates/filter'
import { useCandidateFilter } from '@/features/candidates/useCandidateFilter'
import { useCandidateImport } from '@/features/candidates/useCandidateImport'
import { useCandidates } from '@/features/candidates/useCandidates'
import { useFormResponseImport, type FormImportChange } from '@/features/import-form/useFormResponseImport'
import type { FormLayoutColumn } from '@/features/form-layout/api'
import { useFormLayout } from '@/features/form-layout/useFormLayout'
import { useIntakeRounds, roundDisplayName } from '@/features/intake-rounds/useIntakeRounds'
import { sendScope } from '@/features/prepared-messages/format'
import { useEmailTemplates } from '@/features/email-templates/useEmailTemplates'
import { useTemplateSources } from '@/features/email-templates/useTemplateSources'
import { usePreparedMessages } from '@/features/prepared-messages/usePreparedMessages'
import { usePromoteCandidates } from '@/features/promote-candidates/usePromoteCandidates'
import type { VacancyRound } from '@/features/vacancies/api'
import { progressPercent } from '@/features/vacancies/format'
import { hiringShortage } from '@/features/vacancies/hiring'
import { problemMessage, problemMessageText } from '@/shared/problem-details'
import { useCloseVacancy } from './useCloseVacancy'
import { useVacancyDetail } from './useVacancyDetail'

export function useVacancyDetailFlow(
  vacancyId: Ref<string>,
  initialCandidateFilters: Partial<CandidateFilterState> = {},
) {
  const { vacancy, loadError, viewState, load } = useVacancyDetail(vacancyId)

  const rounds = computed<VacancyRound[]>(() => vacancy.value?.rounds ?? [])
  const closedRounds = computed(() =>
    rounds.value
      .filter((round) => round.status === 'closed')
      .sort((left, right) => {
        const leftClosedAt = left.closedAt ? Date.parse(left.closedAt) : Number.NEGATIVE_INFINITY
        const rightClosedAt = right.closedAt ? Date.parse(right.closedAt) : Number.NEGATIVE_INFINITY
        return rightClosedAt - leftClosedAt || right.roundNumber - left.roundNumber
      }),
  )

  const {
    creating: creatingRound,
    closing: closingRound,
    activeRound,
    canCreateRound,
    create: createRoundAction,
    close: closeRoundAction,
  } = useIntakeRounds(rounds, vacancyId, load)

  const selectedRoundId = shallowRef<number | null>(null)
  const selectedRound = computed(
    () => rounds.value.find((round) => round.id === selectedRoundId.value) ?? null,
  )
  const selectedRoundParam = computed(() =>
    selectedRoundId.value === null ? '' : String(selectedRoundId.value),
  )

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
  const canOpenRound = computed(() => !isClosed.value && canCreateRound.value)

  const { closing: closingVacancy, close: closeVacancyAction } = useCloseVacancy(vacancy, load)

  const {
    candidates,
    loadError: candidatesError,
    viewState: candidatesViewState,
    removing,
    load: loadCandidates,
    remove,
  } = useCandidates(vacancyId, selectedRoundParam)
  const refreshVacancyAndCandidates = async () => {
    await Promise.all([load(), loadCandidates()])
  }
  // The Form Layout loads beside the vacancy; editing it re-projects stored
  // rows, and guided import / drift confirm stamp it from the uploaded file.
  const formLayoutFlow = useFormLayout(vacancyId)
  const refreshVacancyCandidatesAndLayout = async () => {
    await Promise.all([load(), loadCandidates(), formLayoutFlow.load()])
  }
  const {
    importing,
    importError,
    results,
    importFiles: importCandidateFiles,
    clearError,
  } = useCandidateImport(vacancyId, selectedRoundParam, refreshVacancyAndCandidates)
  const formImportChanged = async (changed: FormImportChange) => {
    if (changed === 'candidatesAndLayout') {
      await refreshVacancyCandidatesAndLayout()
    } else {
      await refreshVacancyAndCandidates()
    }
  }
  const formImport = useFormResponseImport(
    vacancyId,
    selectedRoundParam,
    formLayoutFlow.layout,
    formImportChanged,
  )
  const {
    status: statusFilter,
    outcome: outcomeFilter,
    query: searchQuery,
    receivedSort,
    statusCounts,
    outcomeCounts,
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

  const canPromote = computed(
    () =>
      !isClosed.value &&
      selectedRound.value?.status === 'open' &&
      activeRound.value?.id === selectedRound.value?.id &&
      closedRounds.value.length > 0,
  )

  async function importFiles(files: File[]): Promise<boolean> {
    const response = await importCandidateFiles(files)
    if (!response) {
      return false
    }
    // Refresh so the vacancy progress, round counts, and the candidate list reflect the import.
    await refreshVacancyAndCandidates()
    return true
  }

  // A layout save re-projects over stored rows, so candidate details change too.
  async function saveFormLayoutMapping(columns: FormLayoutColumn[]): Promise<boolean> {
    const saved = await formLayoutFlow.save(columns)
    if (saved) {
      await refreshVacancyAndCandidates()
    }
    return saved
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
    selectedRoundId.value = created.id
    return true
  }

  async function closeRound(round: VacancyRound): Promise<boolean> {
    return closeRoundAction(round)
  }

  async function deleteCandidate(candidate: CandidateSummary): Promise<string | null> {
    try {
      await remove(candidate)
      await refreshVacancyAndCandidates()
      return null
    } catch (error) {
      const message = problemMessage(error, 'Something went wrong')
      if (message.kind !== 'failure') {
        await refreshVacancyAndCandidates()
      }
      return problemMessageText(message)
    }
  }

  const promotion = usePromoteCandidates(
    vacancyId,
    activeRound,
    closedRounds,
    canPromote,
    refreshVacancyAndCandidates,
  )

  const preparedOpen = shallowRef(false)
  const preparedCandidate = shallowRef<CandidateSummary | null>(null)
  const emailTemplatesOpen = shallowRef(false)
  const templatesRevision = shallowRef(0)

  watch(emailTemplatesOpen, (isOpen, wasOpen) => {
    if (wasOpen && !isOpen) {
      templatesRevision.value += 1
    }
  })

  const sendCandidates = computed(() =>
    sendScope(preparedCandidate.value, candidates.value ?? []),
  )
  const sendRoundName = computed(() => {
    const round = selectedRound.value
    return round ? roundDisplayName(round) : 'the selected round'
  })

  const messages = usePreparedMessages(
    vacancyId,
    sendCandidates,
    preparedOpen,
    templatesRevision,
  )

  const emailTemplates = useEmailTemplates(vacancyId)
  const templateSources = useTemplateSources(vacancyId)

  function openPrepared(candidate?: CandidateSummary) {
    preparedCandidate.value = candidate ?? null
    preparedOpen.value = true
  }

  function openEmailTemplates() {
    emailTemplatesOpen.value = true
  }

  return {
    vacancy: {
      vacancy,
      loadError,
      viewState,
      load,
      progress,
      vacancyRequirements,
      isClosed,
      closingVacancy,
      closeVacancy: closeVacancyAction,
    },
    rounds: {
      rounds,
      closedRounds,
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
      outcomeFilter,
      searchQuery,
      receivedSort,
      statusCounts,
      outcomeCounts,
      filteredCandidates,
      listState,
      toggleReceivedSort,
      clearFilters,
      candidatesReadonly,
      deleteCandidate,
    },
    promote: {
      canPromote,
      open: promotion.open,
      sourceRoundId: promotion.sourceRoundId,
      promotableCandidates: promotion.promotableCandidates,
      sourceLoading: promotion.sourceLoading,
      sourceError: promotion.sourceError,
      selectedCandidateIds: promotion.selectedCandidateIds,
      submitting: promotion.submitting,
      submitError: promotion.submitError,
      canSubmit: promotion.canSubmit,
      openDialog: promotion.openDialog,
      submit: promotion.submit,
    },
    prepared: {
      preparedOpen,
      emailTemplatesOpen,
      sendCandidates,
      sendRoundName,
      openPrepared,
      openEmailTemplates,
      messages,
      emailTemplates,
      templateSources,
    },
    import: {
      importing,
      importError,
      results,
      clearError,
      canImport,
      importFiles,
      formImport,
    },
    formLayout: {
      layout: formLayoutFlow.layout,
      loadError: formLayoutFlow.loadError,
      viewState: formLayoutFlow.viewState,
      saving: formLayoutFlow.saving,
      saveError: formLayoutFlow.saveError,
      load: formLayoutFlow.load,
      save: saveFormLayoutMapping,
    },
  }
}
