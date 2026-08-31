<script setup lang="ts">
import { computed, shallowRef, toRef } from 'vue'
import AppButton from '@/shared/ui/AppButton.vue'
import AppIcon from '@/shared/ui/AppIcon.vue'
import StatusBadge from './components/StatusBadge.vue'
import ImportCandidatesDialog from './components/ImportCandidatesDialog.vue'
import CandidateDeleteDialog from './components/CandidateDeleteDialog.vue'
import ImportResultList from './components/ImportResultList.vue'
import CandidateList from './components/CandidateList.vue'
import { useVacancyDetail } from './useVacancyDetail'
import { useCandidateImport } from './useCandidateImport'
import { useCandidates } from './useCandidates'
import { formatDate, progressPercent } from './format'
import type { CandidateSummary } from './api'

const props = defineProps<{
  id: string
}>()

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

const importOpen = shallowRef(false)
const deleteOpen = shallowRef(false)
const deletingCandidate = shallowRef<CandidateSummary | null>(null)
const deleteError = shallowRef<string | null>(null)

const isClosed = computed(() => vacancy.value?.status === 'closed')

const progress = computed(() => (vacancy.value ? progressPercent(vacancy.value.progress) : 0))

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
</script>

<template>
  <div class="detail-page">
    <nav class="detail-page__nav" aria-label="Breadcrumb">
      <RouterLink class="detail-page__back" to="/">
        <AppIcon name="arrow-left" :size="16" />
        Vacancies
      </RouterLink>
    </nav>

    <!-- Loading -->
    <div v-if="viewState === 'loading'" class="detail-page__panel" aria-busy="true" aria-label="Loading vacancy">
      <div class="detail-page__loading">
        <span class="skeleton skeleton--title" />
        <span class="skeleton skeleton--line" />
        <span class="skeleton skeleton--block" />
      </div>
    </div>

    <!-- Error -->
    <div v-else-if="viewState === 'error'" class="detail-page__panel detail-page__state" role="alert">
      <p class="detail-page__state-title">Couldn't load vacancy</p>
      <p class="detail-page__state-text">{{ loadError }}</p>
      <AppButton variant="ghost" @click="load">Try again</AppButton>
    </div>

    <template v-else-if="vacancy">
      <header class="detail-page__header">
        <div class="detail-page__heading">
          <h1 class="detail-page__title">{{ vacancy.title }}</h1>
          <p class="detail-page__subtitle">Opened {{ formatDate(vacancy.openedOn) }}</p>
        </div>
        <div class="detail-page__header-side">
          <StatusBadge :status="vacancy.status" />
          <div class="detail-page__actions">
            <AppButton v-if="!isClosed" variant="ghost" @click="openImport">
              <AppIcon name="upload" :size="16" />
              Import .eml
            </AppButton>
            <span title="Available once email templates exist">
              <AppButton disabled>Send Email to all candidate</AppButton>
            </span>
          </div>
        </div>
      </header>

      <section class="detail-page__panel detail-page__progress" aria-label="Vacancy progress">
        <div class="detail-page__progress-info">
          <span class="detail-page__progress-label">Candidates processed</span>
          <span class="detail-page__progress-value">
            {{ vacancy.progress.processedCandidates }}/{{ vacancy.progress.totalCandidates }}
          </span>
        </div>
        <div class="detail-page__progress-track">
          <div class="detail-page__progress-bar" :style="{ width: `${progress}%` }" />
        </div>
      </section>

      <section
        class="detail-page__panel detail-page__candidates"
        :class="{ 'detail-page__candidates--list': candidatesViewState === 'ready' }"
        aria-label="Candidates"
      >
        <div
          v-if="candidatesViewState === 'loading'"
          class="detail-page__candidates-loading"
          aria-busy="true"
          aria-label="Loading candidates"
        >
          <span class="skeleton skeleton--line" />
          <span class="skeleton skeleton--line" />
        </div>

        <div v-else-if="candidatesViewState === 'error'" class="detail-page__notice" role="alert">
          <p class="detail-page__state-title">Couldn't load candidates</p>
          <p class="detail-page__state-text">{{ candidatesError }}</p>
          <AppButton variant="ghost" @click="loadCandidates">Try again</AppButton>
        </div>

        <template v-else-if="candidatesViewState === 'empty'">
          <!-- Closed vacancy is read-only -->
          <div v-if="isClosed" class="detail-page__notice">
            <p class="detail-page__state-title">This vacancy is closed</p>
            <p class="detail-page__state-text">
              A closed vacancy is read-only and can't receive candidate imports.
            </p>
          </div>
          <div v-else class="detail-page__notice">
            <p class="detail-page__state-title">No candidates yet</p>
            <p class="detail-page__state-text">
              Export the application emails as .eml files and drop them in to import each
              email as a candidate.
            </p>
            <AppButton @click="openImport">
              <AppIcon name="upload" :size="16" />
              Import .eml files
            </AppButton>
          </div>
        </template>

        <CandidateList
          v-else
          :candidates="candidates ?? []"
          :readonly="isClosed"
          @remove="requestDeleteCandidate"
        />
      </section>

      <section
        v-if="results && results.length > 0"
        class="detail-page__panel detail-page__results"
        aria-label="Last import results"
      >
        <h2 class="detail-page__results-title">Last import</h2>
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

<style scoped>
.detail-page {
  display: flex;
  flex-direction: column;
  gap: var(--space-6);
}

.detail-page__nav {
  display: flex;
}

.detail-page__back {
  display: inline-flex;
  align-items: center;
  gap: var(--space-2);
  color: var(--muted);
  font-size: var(--text-sm);
  font-weight: 600;
  text-decoration: none;
  border-radius: var(--radius-sm);
  padding: var(--space-1) var(--space-2);
  margin-left: calc(-1 * var(--space-2));
  transition: color 0.15s ease, background 0.15s ease;
}

.detail-page__back:hover {
  color: var(--text);
  background: var(--surface-subtle);
}

.detail-page__header {
  display: flex;
  align-items: flex-start;
  justify-content: space-between;
  gap: var(--space-4);
}

.detail-page__header-side {
  display: flex;
  flex-direction: column;
  align-items: flex-end;
  gap: var(--space-3);
  flex-shrink: 0;
}

.detail-page__actions {
  display: flex;
  align-items: center;
  gap: var(--space-3);
}

.detail-page__heading {
  min-width: 0;
}

.detail-page__title {
  font-size: var(--text-2xl);
  font-weight: 700;
  letter-spacing: -0.02em;
}

.detail-page__subtitle {
  margin: var(--space-1) 0 0;
  color: var(--muted);
  font-size: var(--text-sm);
}

.detail-page__panel {
  background: var(--surface);
  border: 1px solid var(--border);
  border-radius: var(--radius);
  box-shadow: var(--shadow);
}

.detail-page__state {
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  text-align: center;
  gap: var(--space-2);
  padding: var(--space-10) var(--space-6);
  min-height: 14rem;
}

.detail-page__state-title {
  margin: 0;
  font-size: var(--text-lg);
  font-weight: 600;
  color: var(--text);
}

.detail-page__state-text {
  margin: 0;
  color: var(--muted);
  font-size: var(--text-sm);
  max-width: 26rem;
}

.detail-page__state .btn {
  margin-top: var(--space-3);
}

.detail-page__progress {
  display: flex;
  flex-direction: column;
  gap: var(--space-3);
  padding: var(--space-4) var(--space-5);
}

.detail-page__progress-info {
  display: flex;
  align-items: baseline;
  justify-content: space-between;
  gap: var(--space-4);
}

.detail-page__progress-label {
  font-size: var(--text-sm);
  font-weight: 500;
  color: var(--muted);
}

.detail-page__progress-value {
  font-size: var(--text-sm);
  font-weight: 600;
  color: var(--text);
  font-variant-numeric: tabular-nums;
}

.detail-page__progress-track {
  height: 0.4rem;
  border-radius: var(--radius-pill);
  background: var(--neutral-chip-bg);
  overflow: hidden;
}

.detail-page__progress-bar {
  height: 100%;
  border-radius: var(--radius-pill);
  background: var(--accent);
  transition: width 0.3s ease;
}

.detail-page__candidates {
  padding: var(--space-3);
}

.detail-page__candidates--list {
  padding: 0;
}

.detail-page__candidates-loading {
  display: flex;
  flex-direction: column;
  gap: var(--space-3);
  padding: var(--space-4) var(--space-3);
}

.detail-page__notice {
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  text-align: center;
  gap: var(--space-2);
  padding: var(--space-10) var(--space-6);
  min-height: 12rem;
}

.detail-page__notice .btn {
  margin-top: var(--space-3);
}

.detail-page__results {
  padding: var(--space-2) 0;
}

.detail-page__results-title {
  font-size: var(--text-xs);
  font-weight: 600;
  text-transform: uppercase;
  letter-spacing: 0.06em;
  color: var(--muted);
  padding: var(--space-2) var(--space-5) var(--space-3);
  border-bottom: 1px solid var(--border);
}

/* Loading skeleton */
.detail-page__loading {
  display: flex;
  flex-direction: column;
  gap: var(--space-4);
  padding: var(--space-6);
}

.skeleton {
  display: block;
  border-radius: var(--radius-pill);
  background: linear-gradient(90deg, var(--border) 25%, var(--surface-subtle) 50%, var(--border) 75%);
  background-size: 200% 100%;
  animation: shimmer 1.4s infinite;
}

.skeleton--title {
  width: 35%;
  height: 1.4rem;
}

.skeleton--line {
  width: 60%;
  height: 0.8rem;
}

.skeleton--block {
  width: 100%;
  height: 12rem;
  border-radius: var(--radius);
}

@keyframes shimmer {
  to {
    background-position: -200% 0;
  }
}

@media (prefers-reduced-motion: reduce) {
  .skeleton {
    animation: none;
  }
}
</style>
