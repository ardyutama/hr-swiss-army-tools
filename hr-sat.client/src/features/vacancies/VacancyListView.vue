<script setup lang="ts">
import { shallowRef } from 'vue'
import { toast } from 'vue-sonner'
import AppButton from '@/shared/ui/AppButton.vue'
import AppIcon from '@/shared/ui/AppIcon.vue'
import StatCard from '@/shared/ui/StatCard.vue'
import VacancyTable from './components/VacancyTable.vue'
import VacancyFormDialog from './components/VacancyFormDialog.vue'
import ConfirmDeleteDialog from './components/ConfirmDeleteDialog.vue'
import { useVacancies } from './useVacancies'
import type { VacancySummary } from './api'

const { vacancies, loadError, viewState, openCount, cvsToSort, load, remove } = useVacancies()

const formOpen = shallowRef(false)
const editingVacancy = shallowRef<VacancySummary | null>(null)
const deleteOpen = shallowRef(false)
const deletingVacancy = shallowRef<VacancySummary | null>(null)
const deleting = shallowRef(false)
const deleteError = shallowRef<string | null>(null)

function openCreate() {
  editingVacancy.value = null
  formOpen.value = true
}

function openEdit(row: VacancySummary) {
  editingVacancy.value = row
  formOpen.value = true
}

function requestDelete(row: VacancySummary) {
  deletingVacancy.value = row
  deleteError.value = null
  deleteOpen.value = true
}

function closeDelete() {
  deleteOpen.value = false
  deleteError.value = null
}

async function confirmDelete() {
  const vacancy = deletingVacancy.value
  if (!vacancy) {
    return
  }
  deleting.value = true
  deleteError.value = null
  try {
    await remove(vacancy.id)
    toast.success(`Vacancy "${vacancy.title}" deleted successfully`, { closeButton: true })
    deleteOpen.value = false
    deletingVacancy.value = null
  } catch (error) {
    deleteError.value = error instanceof Error ? error.message : 'Failed to delete vacancy'
  } finally {
    deleting.value = false
  }
}

async function onSaved() {
  await load()
}
</script>

<template>
  <div class="vacancy-page">
    <header class="vacancy-page__top">
      <div class="vacancy-page__heading">
        <h1 class="vacancy-page__title">Vacancies</h1>
        <p class="vacancy-page__subtitle">
          Each vacancy collects and sorts its own candidates.
        </p>
      </div>
      <AppButton class="vacancy-page__add" @click="openCreate">
        <AppIcon name="plus" :size="16" />
        Add vacancy
      </AppButton>
    </header>

    <section class="vacancy-page__stats" aria-label="Vacancy statistics">
      <StatCard label="Open vacancies" :value="openCount" />
      <StatCard label="CVs to sort" :value="cvsToSort" hint="across all vacancies" />
    </section>

    <section class="vacancy-page__panel">
      <!-- Loading -->
      <div v-if="viewState === 'loading'" class="vacancy-page__loading" aria-busy="true" aria-label="Loading vacancies">
        <div v-for="n in 4" :key="n" class="skeleton-row">
          <span class="skeleton skeleton--title" />
          <span class="skeleton skeleton--chip" />
          <span class="skeleton skeleton--bar" />
          <span class="skeleton skeleton--date" />
        </div>
      </div>

      <!-- Error -->
      <div v-else-if="viewState === 'error'" class="vacancy-page__state" role="alert">
        <p class="vacancy-page__state-title">Couldn't load vacancies</p>
        <p class="vacancy-page__state-text">{{ loadError }}</p>
        <AppButton variant="ghost" @click="load">Try again</AppButton>
      </div>

      <!-- Empty -->
      <div v-else-if="viewState === 'empty'" class="vacancy-page__state vacancy-page__state--empty">
        <span class="vacancy-page__empty-icon">
          <AppIcon name="briefcase" :size="30" />
        </span>
        <p class="vacancy-page__state-title">No vacancies yet</p>
        <p class="vacancy-page__state-text">
          Create your first vacancy to start collecting and sorting CVs.
        </p>
        <AppButton @click="openCreate">
          <AppIcon name="plus" :size="16" />
          Add your first vacancy
        </AppButton>
      </div>

      <!-- Data -->
      <VacancyTable v-else :rows="vacancies ?? []" @edit="openEdit" @remove="requestDelete" />
    </section>

    <VacancyFormDialog
      :open="formOpen"
      :vacancy="editingVacancy"
      @close="formOpen = false"
      @saved="onSaved"
    />
    <ConfirmDeleteDialog
      :open="deleteOpen"
      :vacancy="deletingVacancy"
      :deleting="deleting"
      :error="deleteError"
      @close="closeDelete"
      @confirm="confirmDelete"
    />
  </div>
</template>

<style scoped>
.vacancy-page {
  display: flex;
  flex-direction: column;
  gap: var(--space-6);
}

.vacancy-page__top {
  display: flex;
  align-items: flex-start;
  justify-content: space-between;
  gap: var(--space-4);
}

.vacancy-page__heading {
  min-width: 0;
}

.vacancy-page__title {
  font-size: var(--text-2xl);
  font-weight: 700;
  letter-spacing: -0.02em;
}

.vacancy-page__subtitle {
  margin: var(--space-1) 0 0;
  color: var(--muted);
  font-size: var(--text-sm);
}

.vacancy-page__add {
  flex-shrink: 0;
  margin-top: var(--space-1);
}

.vacancy-page__stats {
  display: flex;
  gap: var(--space-4);
  flex-wrap: wrap;
}

.vacancy-page__panel {
  --vtable-columns: minmax(0, 2.2fr) minmax(0, 1fr) minmax(0, 1.6fr) minmax(0, 1fr) 5rem;
  background: var(--surface);
  border: 1px solid var(--border);
  border-radius: var(--radius);
  box-shadow: var(--shadow);
  padding: var(--space-2) 0;
  min-height: 22rem;
}

/* States */
.vacancy-page__state {
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  text-align: center;
  gap: var(--space-2);
  padding: var(--space-10) var(--space-6);
  min-height: 22rem;
}

.vacancy-page__state-title {
  margin: 0;
  font-size: var(--text-lg);
  font-weight: 600;
  color: var(--text);
}

.vacancy-page__state-text {
  margin: 0;
  color: var(--muted);
  font-size: var(--text-sm);
  max-width: 26rem;
}

.vacancy-page__state .btn {
  margin-top: var(--space-3);
}

.vacancy-page__empty-icon {
  display: inline-flex;
  align-items: center;
  justify-content: center;
  width: 4rem;
  height: 4rem;
  border-radius: var(--radius);
  background: var(--accent-soft);
  color: var(--accent);
  margin-bottom: var(--space-2);
}

/* Loading skeleton */
.vacancy-page__loading {
  display: flex;
  flex-direction: column;
  padding: var(--space-2) 0;
}

.skeleton-row {
  display: grid;
  grid-template-columns: var(--vtable-columns);
  gap: var(--space-4);
  align-items: center;
  padding: var(--space-4) var(--space-5);
  border-bottom: 1px solid var(--border);
}

.skeleton-row:last-child {
  border-bottom: none;
}

.skeleton {
  display: block;
  height: 0.8rem;
  border-radius: var(--radius-pill);
  background: linear-gradient(90deg, var(--border) 25%, var(--surface-subtle) 50%, var(--border) 75%);
  background-size: 200% 100%;
  animation: shimmer 1.4s infinite;
}

.skeleton--title {
  width: 55%;
  height: 1rem;
}

.skeleton--chip {
  width: 4rem;
  height: 1.3rem;
}

.skeleton--bar {
  width: 80%;
}

.skeleton--date {
  width: 5rem;
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
