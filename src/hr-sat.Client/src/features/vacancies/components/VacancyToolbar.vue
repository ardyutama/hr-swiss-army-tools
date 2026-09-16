<script setup lang="ts">
import type { VacancyStatusFilter } from '../useVacancies'

const props = defineProps<{
  counts: { open: number; closed: number }
}>()

const status = defineModel<VacancyStatusFilter>('status', { required: true })
const query = defineModel<string>('query', { required: true })

const chips: { value: VacancyStatusFilter; label: string }[] = [
  { value: 'open', label: 'Open' },
  { value: 'closed', label: 'Closed' },
  { value: 'all', label: 'All' },
]

function countFor(value: VacancyStatusFilter): number {
  return value === 'all' ? props.counts.open + props.counts.closed : props.counts[value]
}
</script>

<template>
  <div class="flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between">
    <div
      class="flex flex-wrap items-center gap-2"
      role="group"
      aria-label="Filter by vacancy status"
    >
      <UButton
        v-for="chip in chips"
        :key="chip.value"
        :color="status === chip.value ? 'primary' : 'neutral'"
        :variant="status === chip.value ? 'solid' : 'outline'"
        :aria-pressed="status === chip.value"
        size="sm"
        class="rounded-full"
        @click="status = chip.value"
      >
        {{ chip.label }}
        <span
          class="ml-1 tabular-nums"
          :class="status === chip.value ? 'opacity-75' : 'text-muted'"
        >{{ countFor(chip.value) }}</span>
      </UButton>
    </div>
    <UInput
      v-model="query"
      icon="i-lucide-search"
      placeholder="Search by title"
      aria-label="Search vacancies"
      class="sm:w-72"
    />
  </div>
</template>
