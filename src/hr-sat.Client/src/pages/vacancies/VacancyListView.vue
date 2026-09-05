<script setup lang="ts">
import { shallowRef, useTemplateRef } from 'vue'
import VacancyTable from '@/features/vacancies/components/VacancyTable.vue'
import VacancyFormDialog from '@/features/vacancies/components/VacancyFormDialog.vue'
import ConfirmDeleteDialog from '@/features/vacancies/components/ConfirmDeleteDialog.vue'
import { useVacancies } from '@/features/vacancies/useVacancies'
import type { VacancySummary, VacancyWritePayload } from '@/features/vacancies/api'

const {
  vacancies,
  loadError,
  viewState,
  openCount,
  cvsToSort,
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
    closeForm()
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
    deleteError.value = error instanceof Error ? error.message : 'Failed to delete vacancy'
  }
}
</script>

<template>
  <div class="flex flex-col gap-6">
    <header class="flex items-start justify-between gap-4">
      <div class="min-w-0">
        <h1 class="text-2xl font-bold tracking-tight">Vacancies</h1>
        <p class="mt-1 text-sm text-muted">
          Each vacancy collects and sorts its own candidates.
        </p>
      </div>
      <UButton icon="i-lucide-plus" class="shrink-0 mt-1" @click="openCreate">
        Add vacancy
      </UButton>
    </header>

    <section class="flex gap-4 flex-wrap" aria-label="Vacancy statistics">
      <UCard class="flex-1 min-w-44 max-w-64">
        <div class="flex flex-col gap-1">
          <span class="text-sm font-medium text-muted">Open vacancies</span>
          <span class="text-2xl font-bold tracking-tight text-highlighted">{{ openCount }}</span>
        </div>
      </UCard>
      <UCard class="flex-1 min-w-44 max-w-64">
        <div class="flex flex-col gap-1">
          <span class="text-sm font-medium text-muted">CVs to sort</span>
          <span class="text-2xl font-bold tracking-tight text-highlighted">{{ cvsToSort }}</span>
          <span class="text-xs text-dimmed">across all vacancies</span>
        </div>
      </UCard>
    </section>

    <section class="rounded-xl border border-default bg-default shadow-sm py-2 min-h-88">
      <!-- Loading -->
      <div
        v-if="viewState === 'loading'"
        class="flex flex-col"
        aria-busy="true"
        aria-label="Loading vacancies"
      >
        <div
          v-for="n in 4"
          :key="n"
          class="flex items-center gap-4 px-5 py-4 border-b border-default last:border-b-0"
        >
          <USkeleton class="h-4 w-1/3" />
          <USkeleton class="h-5 w-16 rounded-full" />
          <USkeleton class="h-2 w-1/4 rounded-full" />
          <USkeleton class="h-4 w-20" />
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

      <!-- Data -->
      <VacancyTable v-else :rows="vacancies ?? []" @edit="openEdit" @remove="requestDelete" />
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
