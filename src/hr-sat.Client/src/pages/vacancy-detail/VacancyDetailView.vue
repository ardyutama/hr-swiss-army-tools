<script setup lang="ts">
import { shallowRef, toRef, watch } from 'vue'
import { useRouter } from 'vue-router'
import StatusBadge from '@/features/vacancies/components/StatusBadge.vue'
import HiringPlanSummary from '@/features/vacancies/components/HiringPlanSummary.vue'
import ImportCandidatesDialog from '@/features/candidates/components/ImportCandidatesDialog.vue'
import CandidateDeleteDialog from '@/features/candidates/components/CandidateDeleteDialog.vue'
import ImportResultList from '@/features/candidates/components/ImportResultList.vue'
import CandidateList from '@/features/candidates/components/CandidateList.vue'
import CandidateToolbar from '@/features/candidates/components/CandidateToolbar.vue'
import RoundList from '@/features/intake-rounds/components/RoundList.vue'
import CreateRoundDialog from '@/features/intake-rounds/components/CreateRoundDialog.vue'
import CloseRoundDialog from '@/features/intake-rounds/components/CloseRoundDialog.vue'
import { useVacancyDetailFlow } from '@/features/vacancy-detail/useVacancyDetailFlow'
import { formatDate } from '@/features/vacancies/format'
import type { CandidateSummary } from '@/features/candidates/api'
import type { VacancyRound } from '@/features/vacancies/api'

const props = defineProps<{
  id: string
}>()

const router = useRouter()

// Composition surface only: the flow owns vacancy-detail state, rules, and
// coordination; this view connects routing and renders the returned state.
const {
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
} = useVacancyDetailFlow(toRef(props, 'id'))

const importOpen = shallowRef(false)
const deleteOpen = shallowRef(false)
const deletingCandidate = shallowRef<CandidateSummary | null>(null)
const deleteError = shallowRef<string | null>(null)
const createRoundOpen = shallowRef(false)
const closeRoundOpen = shallowRef(false)
const roundToClose = shallowRef<VacancyRound | null>(null)

watch(importOpen, (open) => {
  if (!open) {
    clearError()
  }
})

watch(deleteOpen, (open) => {
  if (!open) {
    deletingCandidate.value = null
    deleteError.value = null
  }
})

watch(closeRoundOpen, (open) => {
  if (!open) {
    roundToClose.value = null
  }
})

function openImport() {
  importOpen.value = true
}

async function onFiles(files: File[]) {
  if (await importFiles(files)) {
    importOpen.value = false
  }
}

function openCreateRound() {
  createRoundOpen.value = true
}

async function onCreateRoundSubmit(payload: { name: string | null }) {
  if (await createRound(payload.name)) {
    createRoundOpen.value = false
  }
}

function requestCloseRound(round: VacancyRound) {
  roundToClose.value = round
  closeRoundOpen.value = true
}

async function confirmCloseRound(round: VacancyRound) {
  if (await closeRound(round)) {
    closeRoundOpen.value = false
  }
}

function requestDeleteCandidate(candidate: CandidateSummary) {
  deletingCandidate.value = candidate
  deleteError.value = null
  deleteOpen.value = true
}

async function confirmDeleteCandidate(candidate: CandidateSummary) {
  const error = await deleteCandidate(candidate)
  if (!error) {
    deleteOpen.value = false
  } else {
    deleteError.value = error
  }
}

function openReview(candidate: CandidateSummary) {
  if (!router.hasRoute('candidate-review') || selectedRoundId.value === null) {
    return
  }

  void router.push({
    name: 'candidate-review',
    params: { id: props.id, roundId: selectedRoundId.value, candidateId: candidate.id },
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

    <!-- Error -->
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
              v-if="canImport"
              color="neutral"
              variant="ghost"
              icon="i-lucide-upload"
              @click="openImport"
            >
              Import .eml
            </UButton>
            <span title="Available once email templates exist">
              <UButton disabled>Send email to all candidates</UButton>
            </span>
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

      <HiringPlanSummary variant="context" :hiring="vacancy.hiring" />

      <section
        class="overflow-hidden rounded-xl border border-default bg-default shadow-sm"
        :class="listState.kind === 'ready' ? 'p-0' : 'p-3'"
        aria-label="Candidates"
      >
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

        <template v-else-if="listState.kind === 'empty'">
          <!-- Closed vacancy is read-only -->
          <UEmpty
            v-if="listState.reason === 'vacancy-closed'"
            icon="i-lucide-lock-keyhole"
            title="This vacancy is closed"
            description="A closed vacancy is read-only and can't receive candidate imports."
            class="min-h-48 px-6 py-10"
          />
          <!-- No active round: imports are rejected until a round is opened -->
          <UEmpty
            v-else-if="listState.reason === 'no-active-round'"
            icon="i-lucide-archive"
            title="No active round"
            description="Open a new intake round before importing candidates."
            class="min-h-48 px-6 py-10"
            :actions="canOpenRound ? [{ label: 'Open a round', icon: 'i-lucide-plus', onClick: openCreateRound }] : []"
          />
          <!-- Selected round is closed (read-only) -->
          <UEmpty
            v-else-if="listState.reason === 'round-closed'"
            icon="i-lucide-lock-keyhole"
            title="This round is closed"
            description="A closed round is read-only. Open a new round to keep importing."
            class="min-h-48 px-6 py-10"
          />
          <UEmpty
            v-else
            icon="i-lucide-users"
            title="No candidates yet"
            description="Export the application emails as .eml files and drop them in to import each email as a candidate."
            class="min-h-48 px-6 py-10"
            :actions="[{ label: 'Import .eml files', icon: 'i-lucide-upload', onClick: openImport }]"
          />
        </template>

        <template v-else>
          <div class="border-b border-default px-5 py-3">
            <CandidateToolbar
              v-model:status="statusFilter"
              v-model:query="searchQuery"
              :counts="statusCounts"
              :total="(candidates ?? []).length"
            />
          </div>
          <UEmpty
            v-if="filteredCandidates.length === 0"
            icon="i-lucide-search-x"
            title="No candidates match"
            description="Try a different search or clear the filters."
            class="min-h-40 px-6 py-10"
            :actions="[{ label: 'Clear filters', icon: 'i-lucide-x', onClick: clearFilters }]"
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

    <ImportCandidatesDialog
      v-model:open="importOpen"
      :busy="importing"
      :error="importError"
      @files="onFiles"
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
      @confirm="confirmCloseRound"
    />
  </div>
</template>
