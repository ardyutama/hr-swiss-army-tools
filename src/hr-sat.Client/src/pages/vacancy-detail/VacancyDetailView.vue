<script setup lang="ts">
import { computed, shallowRef, toRef, watch } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import StatusBadge from '@/features/vacancies/components/StatusBadge.vue'
import HiringPlanSummary from '@/features/vacancies/components/HiringPlanSummary.vue'
import ImportDialog from '@/features/import/components/ImportDialog.vue'
import FormLayoutDialog from '@/features/form-layout/components/FormLayoutDialog.vue'
import DriftDialog from '@/features/import-form/components/DriftDialog.vue'
import FormLayoutSummary from '@/features/form-layout/components/FormLayoutSummary.vue'
import type { FormLayoutColumn } from '@/features/form-layout/api'
import { useFormLayout } from '@/features/form-layout/useFormLayout'
import CandidateDeleteDialog from '@/features/candidates/components/CandidateDeleteDialog.vue'
import ImportResultList from '@/features/candidates/components/ImportResultList.vue'
import CandidateList from '@/features/candidates/components/CandidateList.vue'
import CandidateToolbar from '@/features/candidates/components/CandidateToolbar.vue'
import { useCandidates } from '@/features/candidates/useCandidates'
import { useCandidateImport } from '@/features/candidates/useCandidateImport'
import { useCandidateFilter } from '@/features/candidates/useCandidateFilter'
import RoundList from '@/features/intake-rounds/components/RoundList.vue'
import CreateRoundDialog from '@/features/intake-rounds/components/CreateRoundDialog.vue'
import CloseRoundDialog from '@/features/intake-rounds/components/CloseRoundDialog.vue'
import { useIntakeRounds, roundDisplayName } from '@/features/intake-rounds/useIntakeRounds'
import PromoteCandidatesDialog from '@/features/promote-candidates/components/PromoteCandidatesDialog.vue'
import { usePromoteCandidates } from '@/features/promote-candidates/usePromoteCandidates'
import EmailTemplatesDialog from '@/features/email-templates/components/EmailTemplatesDialog.vue'
import { useEmailTemplates } from '@/features/email-templates/useEmailTemplates'
import { useTemplateSources } from '@/features/email-templates/useTemplateSources'
import PreparedMessageListDialog from '@/features/prepared-messages/components/PreparedMessageListDialog.vue'
import { usePreparedMessages } from '@/features/prepared-messages/usePreparedMessages'
import { sendScope } from '@/features/prepared-messages/format'
import { useFormResponseImport } from '@/features/import-form/useFormResponseImport'
import { candidateFilterQuery, candidateFilterStateFromQuery } from '@/features/candidates/filter'
import { problemMessage, problemMessageText } from '@/shared/problem-details'
import { useActionDialog } from '@/shared/useActionDialog'
import { useVacancyDetail } from '@/features/vacancy-detail/useVacancyDetail'
import { useCloseVacancy } from '@/features/vacancy-detail/useCloseVacancy'
import { useVacancyDetailFlow } from '@/features/vacancy-detail/useVacancyDetailFlow'
import CloseVacancyDialog from '@/features/vacancy-detail/components/CloseVacancyDialog.vue'
import { formatDate, progressPercent } from '@/features/vacancies/format'
import { hiringShortage } from '@/features/vacancies/hiring'
import type { CandidateSummary } from '@/features/candidates/api'
import type { VacancyRound } from '@/features/vacancies/api'
import CandidateListFallback from './CandidateListFallback.vue'

const props = defineProps<{
  id: string
}>()

const router = useRouter()
const route = useRoute()
const vacancyId = toRef(props, 'id')

// The leaf lifecycles, composed here — the view is the composition surface.
const { vacancy, loadError, viewState, load } = useVacancyDetail(vacancyId)
const {
  layout: formLayoutMapping,
  loadError: formLayoutLoadError,
  viewState: formLayoutViewState,
  saving: formLayoutSaving,
  saveError: formLayoutSaveError,
  load: loadFormLayout,
  save: saveFormLayoutMapping,
} = useFormLayout(vacancyId)

const rounds = computed<VacancyRound[]>(() => vacancy.value?.rounds ?? [])
// The composer's refresh announcement reaches the rounds flow lazily: it is
// only ever invoked from a create/close mutation, after composition below.
const roundsFlow = useIntakeRounds(rounds, vacancyId, () => flow.refreshVacancyAndCandidates())
const {
  creating: creatingRound,
  closing: closingRound,
  canCreateRound,
  create: createRoundAction,
  close: closeRound,
} = roundsFlow
const closedRounds = roundsFlow.closedRounds
const activeRound = roundsFlow.activeRound

const flow = useVacancyDetailFlow({
  vacancy,
  rounds,
  activeRound,
  closedRounds,
  reloadVacancy: load,
  // Composed below; the composer invokes its reloads only from mutations and
  // import outcomes, never during setup.
  reloadCandidates: () => candidatesFlow.load(),
  reloadLayout: loadFormLayout,
})
const {
  selectedRoundId,
  selectedRound,
  selectRound,
  isClosed,
  candidatesReadonly,
  canImport,
  canPromote,
} = flow

const selectedRoundParam = computed(() =>
  selectedRoundId.value === null ? '' : String(selectedRoundId.value),
)
const candidatesFlow = useCandidates(vacancyId, selectedRoundParam)
const {
  candidates,
  loadError: candidatesError,
  viewState: candidatesViewState,
  removing,
  load: loadCandidates,
  remove: removeCandidate,
} = candidatesFlow

const selectedRoundClosed = computed(() => selectedRound.value?.status === 'closed')
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
} = useCandidateFilter(
  candidates,
  {
    viewState: candidatesViewState,
    vacancyClosed: isClosed,
    hasActiveRound: computed(() => activeRound.value !== null),
    selectedRoundClosed,
  },
  candidateFilterStateFromQuery(route.query),
)

const { closing: closingVacancy, close: closeVacancy } = useCloseVacancy(vacancy, load)

// The .eml adapter announces its own refresh; a landed import closes the dialog.
const { importing, importError, results, importFiles, clearError } = useCandidateImport(
  vacancyId,
  selectedRoundParam,
  flow.refreshVacancyAndCandidates,
)
const formImport = useFormResponseImport(
  vacancyId,
  selectedRoundParam,
  formLayoutMapping,
  async (changed) => {
    if (changed === 'candidatesAndLayout') {
      await flow.refreshVacancyCandidatesAndLayout()
    } else {
      await flow.refreshVacancyAndCandidates()
    }
  },
)
const formImportUploading = formImport.uploading
const formLayoutPanelHeaders = formImport.panelHeaders

const {
  open: promoteOpen,
  sourceRoundId: promoteSourceRoundId,
  promotableCandidates,
  sourceLoading: promoteSourceLoading,
  sourceError: promoteSourceError,
  selectedCandidateIds: promoteSelectedCandidateIds,
  submitting: promoting,
  submitError: promoteSubmitError,
  canSubmit: canPromoteSubmit,
  openDialog: openPromoteDialog,
  submit: submitPromotions,
} = usePromoteCandidates(
  vacancyId,
  activeRound,
  closedRounds,
  canPromote,
  flow.refreshVacancyAndCandidates,
)

// Vacancy rollup derivations for the header cards and dialogs.
const progress = computed(() => (vacancy.value ? progressPercent(vacancy.value.progress) : 0))
const roundShortage = computed(() => {
  const hiring = vacancy.value?.hiring
  return hiring ? hiringShortage(hiring) : null
})
const vacancyRequirements = computed(
  () => vacancy.value?.requirements.map((requirement) => requirement.phrase) ?? [],
)

// The round manager is view UI state; the chrome flags derive over the flow's.
const roundManagementOpen = shallowRef(false)
const canOpenRound = computed(() => !isClosed.value && canCreateRound.value)
const showRoundChrome = computed(() => rounds.value.length > 1 || roundManagementOpen.value)
const showRoundManager = computed(
  () =>
    !isClosed.value &&
    rounds.value.length === 1 &&
    (activeRound.value !== null || candidatesViewState.value !== 'empty'),
)

function openRoundManager() {
  roundManagementOpen.value = true
}

// Prepared messages / email templates dialog state.
const preparedOpen = shallowRef(false)
const preparedCandidate = shallowRef<CandidateSummary | null>(null)
const emailTemplatesOpen = shallowRef(false)
const templatesRevision = shallowRef(0)

watch(emailTemplatesOpen, (isOpen, wasOpen) => {
  if (wasOpen && !isOpen) {
    templatesRevision.value += 1
  }
})

const sendCandidates = computed(() => sendScope(preparedCandidate.value, candidates.value ?? []))
const sendRoundName = computed(() => {
  const round = selectedRound.value
  return round ? roundDisplayName(round) : 'the selected round'
})

const preparedMessages = usePreparedMessages(
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

const {
  open: importRequested,
  request: requestImport,
  confirm: confirmImport,
} = useActionDialog({
  onReset: () => {
    clearError()
    formImport.clearResult()
  },
})
const {
  open: deleteOpen,
  payload: deletingCandidate,
  error: deleteError,
  request: requestDelete,
  confirm: confirmDelete,
} = useActionDialog<CandidateSummary>()
const {
  open: createRoundOpen,
  request: requestCreateRound,
  confirm: confirmCreateRound,
} = useActionDialog()
const {
  open: closeRoundOpen,
  payload: roundToClose,
  request: requestCloseRoundDialog,
  confirm: confirmCloseRound,
} = useActionDialog<VacancyRound>()
const {
  open: closeVacancyOpen,
  request: requestCloseVacancy,
  confirm: confirmCloseVacancy,
} = useActionDialog()

function openReviewFromPrepared(candidate: CandidateSummary) {
  preparedOpen.value = false
  openReview(candidate)
}

function openImport() {
  requestImport()
}

// The dialog surfaces one alert at a time: the form flow's typed alert wins,
// otherwise the .eml flow's message renders as a red inline alert as before.
const importAlert = computed(() => {
  if (formImport.alert.value) {
    return formImport.alert.value
  }
  return importError.value ? { color: 'error' as const, title: importError.value } : null
})

async function onImportEmlFiles(files: File[]) {
  formImport.clearResult()
  await confirmImport(() => importFiles(files))
}

async function onImportCsvFile(file: File) {
  clearError()
  // The dialog stays open so the summary line remains visible — a refusal
  // moves the step off idle, which swaps it for the guided panel or the drift
  // dialog via the derived bindings below.
  await formImport.importFile(file)
}

// --- Form layout panel + drift dialog --------------------------------------
// The refusal steps and their bindings derive in the import-form module; the
// view keeps only its own intent flags and the v-model wiring.

// Edit mode is view-owned UI state; it loses to any refusal step.
const layoutEditRequested = shallowRef(false)
const layoutEditing = computed({
  get: () => layoutEditRequested.value && formImport.idle.value,
  set: (open: boolean) => {
    layoutEditRequested.value = open
  },
})

// The import dialog shows only while requested and the import sits at idle —
// a refusal closes it; terminal success returns to idle so it reappears with
// the summary line while still requested.
const importDialogOpen = computed({
  get: () => importRequested.value && formImport.idle.value,
  set: (open: boolean) => {
    importRequested.value = open
  },
})

const formLayoutDialogOpen = computed({
  get: () => formImport.guidedSetup.value !== null || layoutEditing.value,
  set: (open: boolean) => {
    if (!open) {
      if (formImport.guidedSetup.value !== null) {
        formImport.cancel()
      }
      layoutEditRequested.value = false
    }
  },
})

const formLayoutDialogMode = computed<'edit' | 'guided'>(() =>
  formImport.guidedSetup.value !== null ? 'guided' : 'edit',
)

const formLayoutPanelBusy = computed(
  () => formImport.guidedSetup.value?.submitting ?? formLayoutSaving.value,
)

const formLayoutPanelAlert = computed(() =>
  formImport.guidedSetup.value !== null ? formImport.alert.value : formLayoutSaveError.value,
)

const formLayoutFileName = computed(() => formImport.guidedSetup.value?.file.name ?? null)

const driftDialogOpen = computed({
  get: () => formImport.headerDrift.value !== null,
  set: (open: boolean) => {
    if (!open && formImport.headerDrift.value !== null) {
      formImport.cancel()
    }
  },
})

const driftChanges = computed(() => formImport.headerDrift.value?.changes ?? [])

const driftSubmitting = computed(() => formImport.headerDrift.value?.submitting ?? false)

function openFormLayoutEdit() {
  layoutEditRequested.value = true
}

function onFormLayoutSave(columns: FormLayoutColumn[]) {
  if (formImport.guidedSetup.value !== null) {
    // Guided setup completes the held file's import; success returns the step
    // to idle, closing the panel and re-opening the import dialog.
    void formImport.submitLayout(columns)
  } else {
    // A layout save re-projects over stored rows, so candidate details change too.
    void saveFormLayoutMapping(columns).then(async (saved) => {
      if (saved) {
        await flow.refreshVacancyAndCandidates()
        layoutEditRequested.value = false
      }
    })
  }
}

function onFormLayoutCancel() {
  if (formImport.guidedSetup.value !== null) {
    // Cancel import drops the held file; nothing is saved.
    formImport.cancel()
  }
  layoutEditRequested.value = false
}

function onDriftConfirm() {
  // Success returns the step to idle: the drift dialog closes and the import
  // dialog reappears with the summary line.
  void formImport.confirmDrift()
}

function onDriftRemap() {
  // The refusal stays held; the guided panel pre-fills the current mapping and
  // saving it completes the import.
  formImport.remap()
}

function onDriftCancel() {
  formImport.cancel()
}

function openCreateRound() {
  requestCreateRound()
}

async function onCreateRoundSubmit(payload: { name: string | null }) {
  await confirmCreateRound(async () => {
    const created = await createRoundAction(payload.name)
    if (created === null) {
      return false
    }
    // The new round becomes the selected one.
    selectRound(created)
    return true
  })
}

function requestCloseRound(round: VacancyRound) {
  requestCloseRoundDialog(round)
}

async function confirmCloseVacancyDialog() {
  await confirmCloseVacancy(() => closeVacancy())
}

async function confirmCloseRoundDialog() {
  await confirmCloseRound(async (round) => (round ? closeRound(round) : false))
}

function requestDeleteCandidate(candidate: CandidateSummary) {
  requestDelete(candidate)
}

async function confirmDeleteCandidate() {
  await confirmDelete(async (candidate) => {
    if (candidate === null) {
      return false
    }
    try {
      await removeCandidate(candidate)
      await flow.refreshVacancyAndCandidates()
      return null
    } catch (error) {
      const message = problemMessage(error, 'Something went wrong')
      if (message.kind !== 'failure') {
        await flow.refreshVacancyAndCandidates()
      }
      return problemMessageText(message)
    }
  })
}

function openReview(candidate: CandidateSummary) {
  if (!router.hasRoute('candidate-review') || selectedRoundId.value === null) {
    return
  }

  void router.push({
    name: 'candidate-review',
    params: { id: props.id, roundId: selectedRoundId.value, candidateId: candidate.id },
    query: candidateFilterQuery({
      status: statusFilter.value,
      outcome: outcomeFilter.value,
      query: searchQuery.value,
      receivedSort: receivedSort.value,
    }),
  })
}
</script>

<template>
  <div class="flex flex-col gap-6">
    <nav class="flex" aria-label="Breadcrumb">
      <RouterLink
        class="-ml-2 inline-flex items-center gap-2 rounded-md px-2 py-1 text-sm font-semibold text-muted transition-colors hover:bg-muted hover:text-highlighted"
        to="/"
      >
        <UIcon name="i-lucide-arrow-left" class="size-4" aria-hidden="true" />
        Vacancies
      </RouterLink>
    </nav>

    <!-- Loading -->
    <div
      v-if="viewState === 'loading'"
      class="rounded-xl border border-default bg-default p-6 shadow-sm"
      aria-busy="true"
      aria-label="Loading vacancy"
    >
      <div class="flex flex-col gap-4">
        <USkeleton class="h-6 w-1/3" />
        <USkeleton class="h-4 w-3/5" />
        <USkeleton class="h-48 w-full rounded-xl" />
      </div>
    </div>

    <UAlert
      v-else-if="viewState === 'error'"
      color="error"
      variant="subtle"
      icon="i-lucide-triangle-alert"
      title="Couldn't load vacancy"
      :description="loadError ?? undefined"
      role="alert"
      :actions="[{ label: 'Try again', color: 'error', variant: 'outline', onClick: load }]"
    />

    <template v-else-if="vacancy">
      <header class="flex flex-col gap-4 sm:flex-row sm:items-start sm:justify-between">
        <div class="min-w-0">
          <h1 class="text-2xl font-bold tracking-tight text-highlighted">{{ vacancy.title }}</h1>
          <p class="mt-1 text-sm text-muted">Opened {{ formatDate(vacancy.openedOn) }}</p>
        </div>
        <div class="flex shrink-0 flex-col items-start gap-3 sm:items-end">
          <StatusBadge :status="vacancy.status" />
          <div class="flex flex-wrap items-center gap-3">
            <UButton
              v-if="showRoundManager && !roundManagementOpen"
              color="neutral"
              variant="ghost"
              icon="i-lucide-layers-2"
              @click="openRoundManager"
            >
              Manage rounds
            </UButton>
            <UButton
              v-if="formLayoutViewState === 'ready' && !isClosed"
              color="neutral"
              variant="ghost"
              icon="i-lucide-table-properties"
              @click="openFormLayoutEdit"
            >
              Form layout
            </UButton>
            <UButton
              v-if="canImport"
              color="neutral"
              variant="ghost"
              icon="i-lucide-upload"
              @click="openImport"
            >
              Import candidates
            </UButton>
            <UButton
              color="neutral"
              variant="ghost"
              icon="i-lucide-mail"
              @click="openPrepared()"
            >
              Send email to all candidates
            </UButton>
            <UButton
              v-if="!isClosed"
              color="error"
              variant="ghost"
              icon="i-lucide-lock"
              @click="requestCloseVacancy()"
            >
              Close vacancy
            </UButton>
          </div>
        </div>
      </header>

      <div class="grid gap-4 lg:grid-cols-2">
        <section
          class="flex flex-col gap-3 rounded-xl border border-default bg-default px-5 py-4 shadow-sm"
          aria-label="Vacancy progress"
        >
          <div class="flex items-baseline justify-between gap-4">
            <span class="text-sm font-medium text-muted">Candidates processed</span>
            <span class="text-sm font-semibold tabular-nums text-highlighted">
              {{ vacancy.progress.processedCandidates }}/{{ vacancy.progress.totalCandidates }}
            </span>
          </div>
          <div class="h-1.5 overflow-hidden rounded-full bg-muted">
            <div
              class="h-full rounded-full bg-primary"
              :style="{ width: `${progress}%` }"
            />
          </div>
        </section>

        <HiringPlanSummary :hiring="vacancy.hiring" />
      </div>

      <RoundList
        v-if="showRoundChrome"
        :rounds="rounds"
        :selected-round-id="selectedRoundId"
        :can-create-round="canOpenRound"
        :creating="creatingRound"
        @select="selectRound"
        @create="openCreateRound"
        @close="requestCloseRound"
      />

      <FormLayoutSummary
        :state="formLayoutViewState"
        :layout="formLayoutMapping"
        :load-error="formLayoutLoadError"
        :can-edit="!isClosed"
        @edit="openFormLayoutEdit"
        @retry="loadFormLayout"
      />

      <HiringPlanSummary variant="context" :hiring="vacancy.hiring" />

      <section
        class="overflow-hidden rounded-xl border border-default bg-default shadow-sm"
        :class="listState.kind === 'ready' ? 'p-0' : 'p-3'"
        aria-label="Candidates"
      >
        <div v-if="canPromote" class="flex justify-end border-b border-default px-5 py-3">
          <UButton
            color="primary"
            variant="soft"
            icon="i-lucide-arrow-up-right"
            @click="openPromoteDialog"
          >
            Promote from…
          </UButton>
        </div>
        <!-- Shape-matched table skeleton (ADR-0008 decision 14) -->
        <div
          v-if="listState.kind === 'loading'"
          class="flex flex-col p-2"
          aria-busy="true"
          aria-label="Loading candidates"
        >
          <div class="flex items-center gap-4 border-b border-default px-3 pb-3 pt-1">
            <USkeleton class="h-3 w-24" />
            <USkeleton class="h-3 w-20" />
            <USkeleton class="h-3 w-28" />
            <USkeleton class="h-3 w-16" />
          </div>
          <div
            v-for="n in 4"
            :key="n"
            class="flex items-center gap-4 border-b border-default px-3 py-4 last:border-b-0"
          >
            <USkeleton class="h-4 w-1/4" />
            <USkeleton class="h-4 w-12" />
            <USkeleton class="h-4 w-1/3" />
            <USkeleton class="h-5 w-20 rounded-full" />
          </div>
        </div>

        <UAlert
          v-else-if="listState.kind === 'error'"
          color="error"
          variant="subtle"
          icon="i-lucide-triangle-alert"
          title="Couldn't load candidates"
          :description="candidatesError ?? undefined"
          role="alert"
          :actions="[{ label: 'Try again', color: 'error', variant: 'outline', onClick: loadCandidates }]"
        />

        <CandidateListFallback
          v-else-if="listState.kind === 'empty'"
          :state="listState"
          :can-open-round="canOpenRound"
          @open-round="openCreateRound"
          @import="openImport"
          @clear-filters="clearFilters"
        />

        <template v-else>
          <div class="border-b border-default px-5 py-3">
            <CandidateToolbar
              v-model:status="statusFilter"
              v-model:outcome="outcomeFilter"
              v-model:query="searchQuery"
              :counts="statusCounts"
              :outcome-counts="outcomeCounts"
              :total="(candidates ?? []).length"
            />
          </div>
          <CandidateListFallback
            v-if="filteredCandidates.length === 0"
            :state="listState"
            :can-open-round="canOpenRound"
            @open-round="openCreateRound"
            @import="openImport"
            @clear-filters="clearFilters"
          />
          <CandidateList
            v-else
            :vacancy-id="id"
            :candidates="filteredCandidates"
            :requirements="vacancyRequirements"
            :received-sort="receivedSort"
            :readonly="candidatesReadonly"
            @remove="requestDeleteCandidate"
            @review="openReview"
            @send="openPrepared"
            @toggle-received-sort="toggleReceivedSort"
          />
        </template>
      </section>

      <section
        v-if="results && results.length > 0"
        class="overflow-hidden rounded-xl border border-default bg-default py-2 shadow-sm"
        aria-label="Last import results"
      >
        <h2 class="border-b border-default px-5 pb-3 pt-2 text-xs font-semibold uppercase tracking-[0.06em] text-muted">
          Last import
        </h2>
        <ImportResultList :results="results" />
      </section>
    </template>

    <ImportDialog
      v-model:open="importDialogOpen"
      :busy="importing || formImportUploading"
      :alert="importAlert"
      :summary="formImport.summaryLine.value"
      @eml-files="onImportEmlFiles"
      @csv-file="onImportCsvFile"
    />
    <FormLayoutDialog
      v-model:open="formLayoutDialogOpen"
      :mode="formLayoutDialogMode"
      :headers="formLayoutPanelHeaders"
      :initial-columns="formLayoutMapping?.columns ?? null"
      :initial-headers="formLayoutMapping?.headerSnapshot ?? null"
      :file-name="formLayoutFileName"
      :busy="formLayoutPanelBusy"
      :alert="formLayoutPanelAlert"
      @save="onFormLayoutSave"
      @cancel="onFormLayoutCancel"
    />
    <DriftDialog
      v-model:open="driftDialogOpen"
      :changes="driftChanges"
      :busy="driftSubmitting"
      :alert="formImport.alert.value"
      @confirm="onDriftConfirm"
      @remap="onDriftRemap"
      @cancel="onDriftCancel"
    />
    <CandidateDeleteDialog
      v-model:open="deleteOpen"
      :candidate="deletingCandidate"
      :deleting="removing"
      :error="deleteError"
      @confirm="confirmDeleteCandidate"
    />
    <CreateRoundDialog
      v-model:open="createRoundOpen"
      :saving="creatingRound"
      @submit="onCreateRoundSubmit"
    />
    <CloseRoundDialog
      v-model:open="closeRoundOpen"
      :round="roundToClose"
      :shortage="roundShortage"
      :closing="closingRound"
      @confirm="confirmCloseRoundDialog"
    />
    <CloseVacancyDialog
      v-model:open="closeVacancyOpen"
      :vacancy="vacancy"
      :closing="closingVacancy"
      @confirm="confirmCloseVacancyDialog"
    />
    <PromoteCandidatesDialog
      v-model:open="promoteOpen"
      v-model:source-round-id="promoteSourceRoundId"
      v-model:selected-candidate-ids="promoteSelectedCandidateIds"
      :rounds="closedRounds"
      :promotable-candidates="promotableCandidates"
      :loading="promoteSourceLoading"
      :error="promoteSourceError"
      :submitting="promoting"
      :submit-error="promoteSubmitError"
      :can-submit="canPromoteSubmit"
      @submit="submitPromotions"
    />
    <PreparedMessageListDialog
      v-model:open="preparedOpen"
      :candidate-count="sendCandidates.length"
      :round-name="sendRoundName"
      :view-state="preparedMessages.viewState.value"
      :load-error="preparedMessages.loadError.value"
      :rows="preparedMessages.rows.value"
      :contactable-count="preparedMessages.contactableCount.value"
      :missing-email="preparedMessages.missingEmail.value"
      :excluded-undecided="preparedMessages.excludedUndecided.value"
      :excluded-outcome="preparedMessages.excludedOutcome.value"
      :missing-template-kinds="preparedMessages.missingTemplateKinds.value"
      :copied-candidate-id="preparedMessages.copiedCandidateId.value"
      @edit-templates="openEmailTemplates"
      @open-review="openReviewFromPrepared"
      @retry="preparedMessages.retry"
      @copy="preparedMessages.copy"
      @reload="preparedMessages.load"
    />
    <EmailTemplatesDialog
      v-if="vacancy"
      v-model:open="emailTemplatesOpen"
      :vacancy-id="id"
      :vacancy-title="vacancy.title"
      :opened-on="vacancy.openedOn"
      :status="vacancy.status"
      :candidates="candidates ?? []"
      :templates="emailTemplates.templates.value"
      :load-error="emailTemplates.loadError.value"
      :view-state="emailTemplates.viewState.value"
      :saving="emailTemplates.saving.value"
      :deleting="emailTemplates.deleting.value"
      :sources="templateSources.sources.value"
      :save="emailTemplates.save"
      :remove="emailTemplates.remove"
      @reload="emailTemplates.load"
      @load-sources="templateSources.loadSources"
    />
  </div>
</template>
