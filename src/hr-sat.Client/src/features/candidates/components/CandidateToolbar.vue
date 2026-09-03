<script setup lang="ts">
import type { CandidateReviewStatus } from '../api'
import type { CandidateStatusFilter } from '../filter'

const props = defineProps<{
  counts: Record<CandidateReviewStatus, number>
  total: number
}>()

const status = defineModel<CandidateStatusFilter>('status', { required: true })
const query = defineModel<string>('query', { required: true })

const chips: { value: CandidateStatusFilter; label: string }[] = [
  { value: 'all', label: 'All' },
  { value: 'new', label: 'New' },
  { value: 'flagged', label: 'Flagged' },
  { value: 'shortlisted', label: 'Shortlisted' },
  { value: 'rejected', label: 'Rejected' },
]

function countFor(value: CandidateStatusFilter): number {
  return value === 'all' ? props.total : props.counts[value]
}
</script>

<template>
  <div class="flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between">
    <div class="flex flex-wrap items-center gap-2" role="group" aria-label="Filter by review status">
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
      placeholder="Name, sender email, or subject"
      aria-label="Search candidates"
      class="sm:w-72"
    />
  </div>
</template>
