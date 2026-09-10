<script setup lang="ts">
import { computed } from 'vue'
import type { CandidateReviewStatus } from '../api'
import type { CandidateOutcomeFilter, CandidateStatusFilter } from '../filter'

const props = defineProps<{
  counts: Record<CandidateReviewStatus, number>
  outcomeCounts: Record<CandidateOutcomeFilter, number>
  total: number
}>()

const status = defineModel<CandidateStatusFilter>('status', { required: true })
const outcome = defineModel<CandidateOutcomeFilter>('outcome', { required: true })
const query = defineModel<string>('query', { required: true })

const chips: { value: CandidateStatusFilter; label: string }[] = [
  { value: 'all', label: 'All' },
  { value: 'new', label: 'New' },
  { value: 'flagged', label: 'Flagged' },
  { value: 'shortlisted', label: 'Shortlisted' },
  { value: 'rejected', label: 'Rejected' },
]

const outcomeChips: { value: CandidateOutcomeFilter; label: string }[] = [
  { value: 'any', label: 'Any outcome' },
  { value: 'undecided', label: 'Bench' },
  { value: 'hired', label: 'Hired' },
  { value: 'runaway', label: 'Runaway' },
  { value: 'declined', label: 'Declined' },
]

const showOutcomeChips = computed(() => status.value === 'all' || status.value === 'shortlisted')

function countFor(value: CandidateStatusFilter): number {
  return value === 'all' ? props.total : props.counts[value]
}

function outcomeCountFor(value: CandidateOutcomeFilter): number {
  return props.outcomeCounts[value]
}
</script>

<template>
  <div class="flex flex-col gap-3">
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
    <div
      v-if="showOutcomeChips"
      class="flex flex-wrap items-center gap-2"
      role="group"
      aria-label="Filter by hire outcome"
    >
      <span class="mr-1 text-xs font-semibold uppercase tracking-[0.06em] text-muted">Outcome</span>
      <UButton
        v-for="chip in outcomeChips"
        :key="chip.value"
        :color="outcome === chip.value ? 'primary' : 'neutral'"
        :variant="outcome === chip.value ? 'solid' : 'outline'"
        :aria-pressed="outcome === chip.value"
        size="sm"
        class="rounded-full"
        @click="outcome = chip.value"
      >
        {{ chip.label }}
        <span
          class="ml-1 tabular-nums"
          :class="outcome === chip.value ? 'opacity-75' : 'text-muted'"
        >{{ outcomeCountFor(chip.value) }}</span>
      </UButton>
    </div>
  </div>
</template>
