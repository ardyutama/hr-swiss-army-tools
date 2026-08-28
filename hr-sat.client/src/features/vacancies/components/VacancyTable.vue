<script setup lang="ts">
import AppButton from '@/shared/ui/AppButton.vue'
import AppTable from '@/shared/ui/AppTable.vue'
import StatusBadge from './StatusBadge.vue'

interface VacancyRow {
  id: string
  role: string
  status: 'Open' | 'Draft' | 'Closed'
  candidates: string
  date: string
}

defineProps<{
  rows: VacancyRow[]
}>()

const emit = defineEmits<{
  edit: [row: VacancyRow]
  remove: [row: VacancyRow]
}>()

const columns = [
  { key: 'role', label: 'Vacancy role' },
  { key: 'status', label: 'Status' },
  { key: 'candidates', label: 'Candidates' },
  { key: 'date', label: 'Date' },
  { key: 'edit', label: 'Edit' },
  { key: 'delete', label: 'Delete' },
]
</script>

<template>
  <AppTable :columns="columns" :rows="rows">
    <template #cell-status="{ row }">
      <StatusBadge :status="row.status" />
    </template>
    <template #cell-edit="{ row }">
      <AppButton variant="ghost" @click="emit('edit', row)">Edit</AppButton>
    </template>
    <template #cell-delete="{ row }">
      <AppButton variant="danger" @click="emit('remove', row)">Delete</AppButton>
    </template>
  </AppTable>
</template>
