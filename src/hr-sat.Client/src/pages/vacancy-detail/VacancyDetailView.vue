<script setup lang="ts">
import { computed, shallowRef, toRef, watch } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import StatusBadge from '@/features/vacancies/components/StatusBadge.vue'
import HiringPlanSummary from '@/features/vacancies/components/HiringPlanSummary.vue'
import ImportDialog from '@/features/import/components/ImportDialog.vue'
import FormLayoutDialog from '@/features/form-layout/components/FormLayoutDialog.vue'
import DriftDialog from '@/features/form-layout/components/DriftDialog.vue'
import FormLayoutSummary from '@/features/form-layout/components/FormLayoutSummary.vue'
import type { FormLayoutColumn } from '@/features/form-layout/api'
import CandidateDeleteDialog from '@/features/candidates/components/CandidateDeleteDialog.vue'
import ImportResultList from '@/features/candidates/components/ImportResultList.vue'
import CandidateList from '@/features/candidates/components/CandidateList.vue'
import CandidateToolbar from '@/features/candidates/components/CandidateToolbar.vue'
import RoundList from '@/features/intake-rounds/components/RoundList.vue'
import CreateRoundDialog from '@/features/intake-rounds/components/CreateRoundDialog.vue'
import CloseRoundDialog from '@/features/intake-rounds/components/CloseRoundDialog.vue'
import PromoteCandidatesDialog from '@/features/promote-candidates/components/PromoteCandidatesDialog.vue'
import EmailTemplatesDialog from '@/features/email-templates/components/EmailTemplatesDialog.vue'
import PreparedMessageListDialog from '@/features/prepared-messages/components/PreparedMessageListDialog.vue'
import { candidateFilterQuery, candidateFilterStateFromQuery } from '@/features/candidates/filter'
import { useActionDialog } from '@/shared/useActionDialog'
import { useVacancyDetailFlow } from '@/features/vacancy-detail/useVacancyDetailFlow'
import CloseVacancyDialog from '@/features/vacancy-detail/components/CloseVacancyDialog.vue'
import { formatDate } from '@/features/vacancies/format'
import type { CandidateSummary } from '@/features/candidates/api'
import type { VacancyRound } from '@/features/vacancies/api'
import CandidateListFallback from './CandidateListFallback.vue'

const props = defineProps<{
  id: string
}>()

const router = useRouter()
const route = useRoute()

const {
  vacancy: {
    vacancy,
    loadError,
    viewState,
    load,
    progress,
    vacancyRequirements,
    isClosed,
    closingVacancy,
    closeVacancy,
  },
  rounds: {
    rounds,
    closedRounds,
    selectedRoundId,
    selectRound,
    canOpenRound,
    roundShortage,
    showRoundChrome,
    showRoundManager,
    roundManagementOpen,
    openRoundManager,
    creatingRound,
    closingRound,
    createRound,
    closeRound,
  },
  promote: {
    canPromote,
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
  },
  prepared: {
    preparedOpen,
    emailTemplatesOpen,
    sendCandidates,
    sendRoundName,
    openPrepared,
    openEmailTemplates,
    messages: preparedMessages,
    emailTemplates,
    templateSources,
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
  import: {
    importing,
    importError,
    results,
    clearError,
    canImport,
    importFiles,
    formImporting,
    formImportError,
    formSummaryLine,
    importFormFile,
    clearFormImportResult,
    formImportRefusal,
    formHeaderChanges,
    importFormWithLayout,
    confirmFormDrift,
    cancelFormRefusal,
  },
  formLayout: {
    layout: formLayoutMapping,
    loadError: formLayoutLoadError,
    viewState: formLayoutViewState,
    saving: formLayoutSaving,
    saveError: formLayoutSaveError,
    load: loadFormLayout,
    save: saveFormLayout,
  },
} = useVacancyDetailFlow(
  toRef(props, 'id'),
  candidateFilterStateFromQuery(route.query),
)

const {
  open: importOpen,
  request: requestImport,
  confirm: confirmImport,
} = useActionDialog({
  onReset: () => {
    clearError()
    clearFormImportResult()
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
  if (formImportError.value) {
    return formImportError.value
  }
  return importError.value ? { color: 'error' as const, title: importError.value } : null
})

async function onImportEmlFiles(files: File[]) {
  clearFormImportResult()
  await confirmImport(() => importFiles(files))
}

async function onImportCsvFile(file: File) {
  clearError()
  // The dialog stays open so the summary line remains visible — unless the
  // import is refused, when the refusal watcher swaps it for the guided panel
  // or the drift dialog.
  await importFormFile(file)
}

// --- Form layout panel + drift dialog --------------------------------------

const formLayoutPanelOpen = shallowRef(false)
const formLayoutPanelMode = shallowRef<'edit' | 'guided'>('edit')
const driftDialogOpen = shallowRef(false)

// A refused form import holds its file in the composable; open the matching
// resolution surface and close the import dialog.
watch(formImportRefusal, (refusal) => {
  if (refusal === null) {
    return
  }
  importOpen.value = false
  if (refusal.changes === null) {
    formLayoutPanelMode.value = 'guided'
    formLayoutPanelOpen.value = true
  } else {
    driftDialogOpen.value = true
  }
})

// Guided mode maps the held file's headers; edit mode maps the saved snapshot.
const formLayoutPanelHeaders = computed(() =>
  formLayoutPanelMode.value === 'guided'
    ? (formImportRefusal.value?.headers ?? formLayoutMapping.value?.headerSnapshot ?? [])
    : (formLayoutMapping.value?.headerSnapshot ?? []),
)

const formLayoutPanelBusy = computed(() =>
  formLayoutPanelMode.value === 'guided' ? formImporting.value : formLayoutSaving.value,
)

const formLayoutPanelAlert = computed(() =>
  formLayoutPanelMode.value === 'guided' ? formImportError.value : formLayoutSaveError.value,
)

function openFormLayoutEdit() {
  formLayoutPanelMode.value = 'edit'
  formLayoutPanelOpen.value = true
}

async function onFormLayoutSave(columns: FormLayoutColumn[]) {
  const guided = formLayoutPanelMode.value === 'guided'
  const succeeded = guided
    ? await importFormWithLayout(columns)
    : await saveFormLayout(columns)
  if (!succeeded) {
    return
  }
  formLayoutPanelOpen.value = false
  if (guided) {
    // Land on the normal import result: toast fired, summary line visible.
    importOpen.value = true
  }
}

function onFormLayoutCancel() {
  if (formLayoutPanelMode.value === 'guided') {
    // Cancel import drops the held file; nothing is saved.
    cancelFormRefusal()
  }
  formLayoutPanelOpen.value = false
}

async function onDriftConfirm() {
  const confirmed = await confirmFormDrift()
  if (!confirmed) {
    return
  }
  driftDialogOpen.value = false
  importOpen.value = true
}

function onDriftRemap() {
  // The refusal stays held; the guided panel pre-fills the current mapping and
  // saving it completes the import.
  driftDialogOpen.value = false
  formLayoutPanelMode.value = 'guided'
  formLayoutPanelOpen.value = true
}

function onDriftCancel() {
  cancelFormRefusal()
  driftDialogOpen.value = false
}

function openCreateRound() {
  requestCreateRound()
}

async function onCreateRoundSubmit(payload: { name: string | null }) {
  await confirmCreateRound(() => createRound(payload.name))
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
  await confirmDelete(async (candidate) => (candidate ? deleteCandidate(candidate) : false))
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
      v-model:open="importOpen"
      :busy="importing || formImporting"
      :alert="importAlert"
      :summary="formSummaryLine"
      @eml-files="onImportEmlFiles"
      @csv-file="onImportCsvFile"
    />
    <FormLayoutDialog
      v-model:open="formLayoutPanelOpen"
      :mode="formLayoutPanelMode"
      :headers="formLayoutPanelHeaders"
      :initial-columns="formLayoutMapping?.columns ?? null"
      :initial-headers="formLayoutMapping?.headerSnapshot ?? null"
      :file-name="formImportRefusal?.file.name ?? null"
      :busy="formLayoutPanelBusy"
      :alert="formLayoutPanelAlert"
      @save="onFormLayoutSave"
      @cancel="onFormLayoutCancel"
    />
    <DriftDialog
      v-model:open="driftDialogOpen"
      :changes="formHeaderChanges"
      :busy="formImporting"
      :alert="formImportError"
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
