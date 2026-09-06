<script setup lang="ts">
import { computed, shallowRef, toRef } from 'vue'
import { useRouter } from 'vue-router'
import StatusBadge from '@/features/vacancies/components/StatusBadge.vue'
import ImportCandidatesDialog from '@/features/candidates/components/ImportCandidatesDialog.vue'
import CandidateDeleteDialog from '@/features/candidates/components/CandidateDeleteDialog.vue'
import ImportResultList from '@/features/candidates/components/ImportResultList.vue'
import CandidateList from '@/features/candidates/components/CandidateList.vue'
import CandidateToolbar from '@/features/candidates/components/CandidateToolbar.vue'
import { useVacancyDetail } from '@/features/vacancy-detail/useVacancyDetail'
import { useCandidateImport } from '@/features/candidates/useCandidateImport'
import { useCandidates } from '@/features/candidates/useCandidates'
import { useCandidateFilter } from '@/features/candidates/useCandidateFilter'
import { formatDate, progressPercent } from '@/features/vacancies/format'
import type { CandidateSummary } from '@/features/candidates/api'

const props = defineProps<{
  id: string
}>()

const router = useRouter()
const vacancyId = toRef(props, 'id')
const { vacancy, loadError, viewState, load } = useVacancyDetail(vacancyId)
const { importing, importError, results, importFiles, clearError } = useCandidateImport(vacancyId)
const {
  candidates,
  loadError: candidatesError,
  viewState: candidatesViewState,
  removing,
  load: loadCandidates,
  remove,
} = useCandidates(vacancyId)
const {
  status: statusFilter,
  query: searchQuery,
  receivedSort,
  statusCounts,
  filteredCandidates,
  toggleReceivedSort,
  clearFilters,
} = useCandidateFilter(candidates)

const importOpen = shallowRef(false)
const deleteOpen = shallowRef(false)
const deletingCandidate = shallowRef<CandidateSummary | null>(null)
const deleteError = shallowRef<string | null>(null)

const isClosed = computed(() => vacancy.value?.status === 'closed')

const progress = computed(() => (vacancy.value ? progressPercent(vacancy.value.progress) : 0))
const vacancyRequirements = computed(() => vacancy.value?.requirements.map((requirement) => requirement.phrase) ?? [])

function openImport() {
  importOpen.value = true
}

function closeImport() {
  importOpen.value = false
  clearError()
}

async function onFiles(files: File[]) {
  const response = await importFiles(files)
  if (response) {
    closeImport()
    // Refresh so the vacancy progress and the candidate list reflect the import.
    await Promise.all([load(), loadCandidates()])
  }
}

function requestDeleteCandidate(candidate: CandidateSummary) {
  deletingCandidate.value = candidate
  deleteError.value = null
  deleteOpen.value = true
}

function closeDelete() {
  deleteOpen.value = false
  deleteError.value = null
}

async function confirmDeleteCandidate() {
  const candidate = deletingCandidate.value
  if (!candidate) {
    return
  }
  try {
    await remove(candidate)
    deleteOpen.value = false
    deletingCandidate.value = null
    // Progress counts candidates, so the vacancy details need a refresh too.
    await load()
  } catch (error) {
    deleteError.value = error instanceof Error ? error.message : 'Failed to delete candidate'
  }
}

// The review route is added by ticket 05; until then the push resolves nowhere.
function openReview(candidate: CandidateSummary) {
  if (!router.hasRoute('candidate-review')) {
    return
  }

  void router.push({
    name: 'candidate-review',
    params: { id: props.id, candidateId: candidate.id },
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
              v-if="!isClosed"
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

      <section
        class="overflow-hidden rounded-xl border border-default bg-default shadow-sm"
        :class="candidatesViewState === 'ready' ? 'p-0' : 'p-3'"
        aria-label="Candidates"
      >
        <!-- Shape-matched table skeleton (ADR-0008 decision 14) -->
        <div
          v-if="candidatesViewState === 'loading'"
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
          v-else-if="candidatesViewState === 'error'"
          color="error"
          variant="subtle"
          icon="i-lucide-triangle-alert"
          title="Couldn't load candidates"
          :description="candidatesError ?? undefined"
          role="alert"
          :actions="[{ label: 'Try again', color: 'error', variant: 'outline', onClick: loadCandidates }]"
        />

        <template v-else-if="candidatesViewState === 'empty'">
          <!-- Closed vacancy is read-only -->
          <UEmpty
            v-if="isClosed"
            icon="i-lucide-lock-keyhole"
            title="This vacancy is closed"
            description="A closed vacancy is read-only and can't receive candidate imports."
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
            :readonly="isClosed"
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
      :open="importOpen"
      :busy="importing"
      :error="importError"
      @close="closeImport"
      @files="onFiles"
    />
    <CandidateDeleteDialog
      :open="deleteOpen"
      :candidate="deletingCandidate"
      :deleting="removing"
      :error="deleteError"
      @close="closeDelete"
      @confirm="confirmDeleteCandidate"
    />
  </div>
</template>
