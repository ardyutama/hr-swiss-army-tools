<script setup lang="ts">
import { shallowRef, useTemplateRef } from 'vue'
import VacancyTable from '@/features/vacancies/components/VacancyTable.vue'
import VacancyFormDialog from '@/features/vacancies/components/VacancyFormDialog.vue'
import ConfirmDeleteDialog from '@/features/vacancies/components/ConfirmDeleteDialog.vue'
import VacancyToolbar from '@/features/vacancies/components/VacancyToolbar.vue'
import { useVacancies } from '@/features/vacancies/useVacancies'
import type { VacancySummary, VacancyWritePayload } from '@/features/vacancies/api'
import { problemMessage, problemMessageText } from '@/shared/problem-details'

const {
  loadError,
  viewState,
  cvsToSort,
  statusFilter,
  searchQuery,
  statusCounts,
  filteredVacancies,
  clearFilters,
  sortKey,
  sortDirection,
  toggleSort,
  sortedVacancies,
  saving,
  removing,
  editingDetails,
  load,
  beginEdit,
  save,
  remove,
} = useVacancies()

const formOpen = shallowRef(false)
const editingVacancy = shallowRef<VacancySummary | null>(null)
const formDialog = useTemplateRef<InstanceType<typeof VacancyFormDialog>>('formDialog')
const deleteOpen = shallowRef(false)
const deletingVacancy = shallowRef<VacancySummary | null>(null)
const deleteError = shallowRef<string | null>(null)

function openCreate() {
  editingVacancy.value = null
  beginEdit(null)
  formOpen.value = true
}

function openEdit(row: VacancySummary) {
  editingVacancy.value = row
  beginEdit(row)
  formOpen.value = true
}

function closeForm() {
  formOpen.value = false
  beginEdit(null)
}

async function onSubmitForm(payload: VacancyWritePayload) {
  try {
    await save(payload, editingVacancy.value?.id ?? null)
    if (editingVacancy.value) {
      formDialog.value?.markSaved()
    } else {
      closeForm()
    }
  } catch (error) {
    formDialog.value?.applyServerErrors(error)
  }
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
  try {
    await remove(vacancy)
    deleteOpen.value = false
    deletingVacancy.value = null
  } catch (error) {
    deleteError.value = problemMessageText(problemMessage(error, 'Something went wrong'))
  }
}
</script>

<template>
  <div class="flex flex-col gap-6">
    <header class="flex items-start justify-between gap-4">
      <div class="min-w-0">
        <h1 class="text-2xl font-bold tracking-tight">Vacancies</h1>
        <p class="mt-1 text-sm text-muted">{{ cvsToSort }} CVs to sort across all vacancies</p>
      </div>
      <UButton icon="i-lucide-plus" class="shrink-0 mt-1" @click="openCreate">
        Add vacancy
      </UButton>
    </header>

    <section class="rounded-xl border border-default bg-default shadow-sm py-2">
      <!-- Shape-matched table skeleton (ADR-0008 decision 14) -->
      <div
        v-if="viewState === 'loading'"
        class="flex flex-col"
        aria-busy="true"
        aria-label="Loading vacancies"
      >
        <div class="flex items-center gap-4 border-b border-default px-5 pb-3 pt-1">
          <USkeleton class="h-3 w-24" />
          <USkeleton class="h-3 w-16" />
          <USkeleton class="h-3 w-28" />
          <USkeleton class="h-3 w-20" />
          <USkeleton class="h-3 w-16" />
          <USkeleton class="h-3 w-10" />
        </div>
        <div
          v-for="n in 4"
          :key="n"
          class="flex items-center gap-4 border-b border-default px-5 py-4 last:border-b-0"
        >
          <USkeleton class="h-4 w-1/4" />
          <USkeleton class="h-5 w-16 rounded-full" />
          <USkeleton class="h-1.5 w-1/4 rounded-full" />
          <USkeleton class="h-4 w-24" />
          <USkeleton class="h-4 w-20" />
          <USkeleton class="h-6 w-12" />
        </div>
      </div>

      <!-- Error -->
      <UAlert
        v-else-if="viewState === 'error'"
        color="error"
        variant="subtle"
        icon="i-lucide-triangle-alert"
        title="Couldn't load vacancies"
        :description="loadError ?? undefined"
        class="m-5"
        role="alert"
        :actions="[{ label: 'Try again', color: 'error', variant: 'outline', onClick: load }]"
      />

      <!-- Empty -->
      <UEmpty
        v-else-if="viewState === 'empty'"
        icon="i-lucide-briefcase"
        title="No vacancies yet"
        description="Create your first vacancy to start collecting and sorting CVs."
        class="min-h-88"
        :actions="[{ label: 'Add your first vacancy', icon: 'i-lucide-plus', onClick: openCreate }]"
      />

      <!-- Ready: toolbar row, then the filtered table or a no-match state -->
      <template v-else>
        <div class="border-b border-default px-5 py-3">
          <VacancyToolbar
            v-model:status="statusFilter"
            v-model:query="searchQuery"
            :counts="statusCounts"
          />
        </div>
        <UEmpty
          v-if="filteredVacancies.length === 0"
          icon="i-lucide-search-x"
          title="No vacancies match these filters"
          description="Try a different search or clear the filters."
          class="min-h-40 px-6 py-10"
          :actions="[{ label: 'Clear filters', icon: 'i-lucide-x', onClick: clearFilters }]"
        />
        <VacancyTable
          v-else
          :rows="sortedVacancies"
          :sort-key="sortKey"
          :sort-direction="sortDirection"
          @edit="openEdit"
          @remove="requestDelete"
          @sort="toggleSort"
        />
      </template>
    </section>

    <VacancyFormDialog
      ref="formDialog"
      :open="formOpen"
      :vacancy="editingVacancy"
      :details="editingDetails"
      :saving="saving"
      @close="closeForm"
      @submit="onSubmitForm"
    />
    <ConfirmDeleteDialog
      :open="deleteOpen"
      :vacancy="deletingVacancy"
      :deleting="removing"
      :error="deleteError"
      @close="closeDelete"
      @confirm="confirmDelete"
    />
  </div>
</template>
