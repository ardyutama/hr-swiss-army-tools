<script setup lang="ts">
import { computed, shallowRef, toRef, watch } from 'vue'
import { useRouter } from 'vue-router'
import StatusBadge from '@/features/vacancies/components/StatusBadge.vue'
import HiringPlanSummary from '@/features/vacancies/components/HiringPlanSummary.vue'
import ImportDialog from '@/features/import/components/ImportDialog.vue'
import GuidedSetupDialog from '@/features/form-layout/components/GuidedSetupDialog.vue'
import FormLayoutEditDialog from '@/features/form-layout/components/FormLayoutEditDialog.vue'
import DriftDialog from '@/features/import-form/components/DriftDialog.vue'
import FormLayoutSummary from '@/features/form-layout/components/FormLayoutSummary.vue'
import CandidateDeleteDialog from '@/features/candidates/components/CandidateDeleteDialog.vue'
import ImportResultList from '@/features/candidates/components/ImportResultList.vue'
import CandidateList from '@/features/candidates/components/CandidateList.vue'
import CandidateToolbar from '@/features/candidates/components/CandidateToolbar.vue'
import { useCandidates } from '@/features/candidates/useCandidates'
import { useMessagingSummary } from '@/features/candidates/useMessagingSummary'
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
import ScreeningSection from '@/features/screening/components/ScreeningSection.vue'
import ScreeningRulesDialog from '@/features/screening/components/ScreeningRulesDialog.vue'
import { useScreeningRules } from '@/features/screening/useScreeningRules'
import { candidateFilterQuery } from '@/features/candidates/filter'
import { problemMessage, problemMessageText } from '@/shared/problem-details'
import { useActionDialog } from '@/shared/useActionDialog'
import { useVacancyDetail } from '@/features/vacancy-detail/useVacancyDetail'
import { useCloseVacancy } from '@/features/vacancy-detail/useCloseVacancy'
import { useVacancyDetailFlow } from '@/features/vacancy-detail/useVacancyDetailFlow'
import { useImportConsole } from '@/features/vacancy-detail/useImportConsole'
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
const vacancyId = toRef(props, 'id')

// The leaf lifecycles, composed here — the view is the composition surface.
const { vacancy, loadError, viewState, load } = useVacancyDetail(vacancyId)

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
  reloadLayout: () => importConsole.formLayout.load(),
  reloadMessaging: () => messagingFlow.load(),
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
const selectedRoundClosed = computed(() => selectedRound.value?.status === 'closed')
const candidatesFlow = useCandidates(vacancyId, selectedRoundParam, {
  vacancyClosed: isClosed,
  hasActiveRound: computed(() => activeRound.value !== null),
  selectedRoundClosed,
})
const {
  response: candidatesResponse,
  listState,
  status: statusFilter,
  outcome: outcomeFilter,
  searchDraft,
  screenedAll,
  page,
  receivedSort,
  toggleReceivedSort,
  clearFilters,
  backToFirstPage,
  pending: candidatesPending,
  loadError: candidatesError,
  viewState: candidatesViewState,
  removing,
  load: loadCandidates,
  remove: removeCandidate,
} = candidatesFlow

// The full unpaged round list feeding the send dialogs and template preview.
const messagingFlow = useMessagingSummary(vacancyId, selectedRoundParam)
const messagingSummaries = messagingFlow.summaries

// Saving rules reclassifies live; the modal-atomic cascade refreshes the list.
const screeningFlow = useScreeningRules(
  vacancyId,
  computed(() => activeRound.value !== null),
  flow.refreshVacancyAndCandidates,
)

const { closing: closingVacancy, close: closeVacancy } = useCloseVacancy(vacancy, load)

// The import console composes the .eml and Form Response channels, the Form
// Layout behind them, and the import-request dialog, and owns what crosses
// between them. The view binds the console's coordinations and the leaves'
// own state directly — the leaves arrive as whole instances; the console
// forwards nothing member-wise (ticket: review adjudication 2026-09-20).
const importConsole = useImportConsole({
  vacancyId,
  roundId: selectedRoundParam,
  refreshVacancyAndCandidates: flow.refreshVacancyAndCandidates,
  refreshVacancyCandidatesAndLayout: flow.refreshVacancyCandidatesAndLayout,
})
const { formImport } = importConsole
const { emlImport: { results } } = importConsole
const {
  layout: formLayoutMapping,
  loadError: formLayoutLoadError,
  viewState: formLayoutViewState,
  saving: formLayoutSaving,
  saveError: formLayoutSaveError,
  load: loadFormLayout,
} = importConsole.formLayout
// Dialog v-models destructure to the top level: a nested member binding would
// overwrite the computeds instead of writing through them. The refusal
// dialogs' open models live on the import-form leaf, derived from its step
// union (pack: dialog visibility).
const {
  requestImport,
  importDialogOpen,
  importAlert,
  importBusy,
  importEmlFiles,
  importCsvFile,
  layoutEditing,
  openLayoutEdit,
  saveLayoutEdit,
} = importConsole
const { guidedSetupOpen, headerDriftOpen } = formImport

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

// The section hugs the toolbar/table edge-to-edge on every filtered state,
// not just ready; the async states keep their padding.
const candidateSectionFlush = computed(() => {
  const kind = listState.value.kind
  return (
    kind === 'ready' ||
    kind === 'no-matches' ||
    kind === 'all-screened' ||
    kind === 'stale-page'
  )
})

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

const sendCandidates = computed(() =>
  sendScope(preparedCandidate.value, messagingSummaries.value ?? []),
)
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
    // The review queue mirrors the visible list, page and screened toggle included.
    query: candidateFilterQuery(candidatesFlow.filters.value),
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
              @click="openLayoutEdit()"
            >
              Form layout
            </UButton>
            <UButton
              v-if="canImport"
              color="neutral"
              variant="ghost"
              icon="i-lucide-upload"
              @click="requestImport()"
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
        @edit="openLayoutEdit()"
        @retry="loadFormLayout"
      />

      <ScreeningSection
        :state="screeningFlow.viewState.value"
        :rule-count="screeningFlow.ruleCount.value"
        :load-error="screeningFlow.loadError.value"
        :has-header-snapshot="(formLayoutMapping?.headerSnapshot.length ?? 0) > 0"
        :screened-out-count="candidatesResponse?.counts.screenedOut ?? null"
        :round-name="selectedRound ? roundDisplayName(selectedRound) : null"
        @edit="screeningFlow.openEditor"
        @retry="screeningFlow.load"
      />

      <HiringPlanSummary variant="context" :hiring="vacancy.hiring" />

      <section
        class="overflow-hidden rounded-xl border border-default bg-default shadow-sm"
        :class="candidateSectionFlush ? 'p-0' : 'p-3'"
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
          @import="requestImport()"
          @clear-filters="clearFilters"
        />

        <template v-else-if="candidatesResponse">
          <div class="border-b border-default px-5 py-3">
            <CandidateToolbar
              v-model:status="statusFilter"
              v-model:outcome="outcomeFilter"
              v-model:query="searchDraft"
              v-model:screened="screenedAll"
              :counts="candidatesResponse.counts"
              :has-form-layout="formLayoutViewState === 'ready'"
            />
          </div>
          <CandidateList
            v-if="listState.kind === 'ready'"
            :vacancy-id="id"
            :candidates="candidatesResponse.items"
            :requirements="vacancyRequirements"
            :received-sort="receivedSort"
            :page="page"
            :page-size="candidatesResponse.pageSize"
            :total="candidatesResponse.filteredTotal"
            :pending="candidatesPending"
            :readonly="candidatesReadonly"
            @remove="requestDeleteCandidate"
            @review="openReview"
            @send="openPrepared"
            @toggle-received-sort="toggleReceivedSort"
            @update:page="page = $event"
          />
          <CandidateListFallback
            v-else
            :state="listState"
            :can-open-round="canOpenRound"
            @open-round="openCreateRound"
            @import="requestImport()"
            @clear-filters="clearFilters"
            @show-screened="screenedAll = true"
            @back-to-first-page="backToFirstPage"
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
      :busy="importBusy"
      :alert="importAlert"
      :summary="formImport.summaryLine.value"
      @eml-files="importEmlFiles"
      @csv-file="importCsvFile"
    />
    <GuidedSetupDialog
      v-model:open="guidedSetupOpen"
      :headers="formImport.guidedSetup.value?.headers ?? []"
      :initial-columns="formLayoutMapping?.columns ?? null"
      :initial-headers="formLayoutMapping?.headerSnapshot ?? null"
      :file-name="formImport.guidedSetup.value?.file.name ?? null"
      :busy="formImport.guidedSetup.value?.submitting ?? false"
      :alert="formImport.alert.value"
      @save="formImport.submitLayout"
      @cancel="formImport.cancel"
    />
    <FormLayoutEditDialog
      v-model:open="layoutEditing"
      :headers="formLayoutMapping?.headerSnapshot ?? []"
      :initial-columns="formLayoutMapping?.columns ?? null"
      :busy="formLayoutSaving"
      :alert="formLayoutSaveError"
      @save="saveLayoutEdit"
    />
    <DriftDialog
      v-model:open="headerDriftOpen"
      :changes="formImport.headerDrift.value?.changes ?? []"
      :busy="formImport.headerDrift.value?.submitting ?? false"
      :alert="formImport.alert.value"
      @confirm="formImport.confirmDrift"
      @remap="formImport.remap"
      @cancel="formImport.cancel"
    />
    <CandidateDeleteDialog
      v-model:open="deleteOpen"
      :candidate="deletingCandidate"
      :deleting="removing"
      :error="deleteError"
      @confirm="confirmDeleteCandidate"
    />
    <ScreeningRulesDialog
      v-model:open="screeningFlow.open.value"
      :header-snapshot="formLayoutMapping?.headerSnapshot ?? []"
      :column-labels="formLayoutMapping?.columns.map((column) => ({ ordinal: column.ordinal, label: column.label })) ?? []"
      :initial-rules="screeningFlow.ruleSet.value?.rules ?? []"
      :preview="screeningFlow.preview.value"
      :preview-failed="screeningFlow.previewFailed.value"
      :has-active-round="activeRound !== null"
      :busy="screeningFlow.saving.value"
      :alert="screeningFlow.saveError.value"
      :readonly="isClosed"
      @save="screeningFlow.save"
      @change="screeningFlow.onDraftChange"
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
      :candidates="messagingSummaries ?? []"
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
