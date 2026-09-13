<script setup lang="ts">
import { formatDate } from '@/features/vacancies/format'
import type { CandidateHireOutcome, CandidateReviewStatus } from '@/features/candidates/api'
import type { LocationQueryRaw } from 'vue-router'

const reviewStatusLabels: Record<CandidateReviewStatus, string> = {
  new: 'New',
  flagged: 'Flagged',
  shortlisted: 'Shortlisted',
  rejected: 'Rejected',
}

const reviewStatusColors: Record<CandidateReviewStatus, 'neutral' | 'warning' | 'success' | 'error'> = {
  new: 'neutral',
  flagged: 'warning',
  shortlisted: 'success',
  rejected: 'error',
}

const hireOutcomeLabels: Record<Exclude<CandidateHireOutcome, 'none'>, string> = {
  hired: 'Hired',
  runaway: 'Runaway',
  declined: 'Declined',
}

const hireOutcomeColors: Record<Exclude<CandidateHireOutcome, 'none'>, 'neutral' | 'success' | 'error'> = {
  hired: 'success',
  runaway: 'error',
  declined: 'neutral',
}

defineProps<{
  vacancyId: string
  title: string
  openedOn: string
  /** 1-based position of the current candidate; 0 while unknown. */
  position: number
  total: number
  processed: number
  progressTotal: number
  backQuery: LocationQueryRaw
  reviewStatus: CandidateReviewStatus
  hireOutcome: CandidateHireOutcome
  isRoundClosed: boolean
  vacancyClosed: boolean
}>()
</script>

<template>
  <header class="flex flex-wrap items-center gap-3">
    <RouterLink
      :to="{ name: 'vacancy-detail', params: { id: vacancyId }, query: backQuery }"
      aria-label="Back to candidate list"
      title="Back to candidate list"
      class="-ml-2 inline-flex size-10 shrink-0 items-center justify-center rounded-xl text-muted transition-colors hover:bg-muted hover:text-highlighted"
    >
      <UIcon name="i-lucide-chevron-left" class="size-5" aria-hidden="true" />
    </RouterLink>
    <div class="min-w-0 flex-1">
      <h1 class="truncate text-xl font-bold tracking-tight text-highlighted">{{ title }}</h1>
      <p class="text-xs text-muted">Opened {{ formatDate(openedOn) }}</p>
    </div>
    <div class="flex shrink-0 flex-wrap items-center justify-end gap-3">
      <div class="text-right">
        <span
          class="block text-sm font-semibold tabular-nums text-highlighted"
          aria-label="Candidate position"
        >
          {{ position }} / {{ total }}
        </span>
        <span class="block text-xs tabular-nums text-muted" aria-label="Vacancy progress">
          {{ processed }} / {{ progressTotal }} reviewed
        </span>
      </div>
      <div v-if="isRoundClosed || vacancyClosed" class="flex flex-wrap justify-end gap-1.5">
        <UBadge
          :color="reviewStatusColors[reviewStatus]"
          variant="subtle"
          class="rounded-full"
          :aria-label="`Review status: ${reviewStatusLabels[reviewStatus]}`"
        >
          {{ reviewStatusLabels[reviewStatus] }}
        </UBadge>
        <UBadge
          v-if="vacancyClosed && hireOutcome !== 'none'"
          :color="hireOutcomeColors[hireOutcome]"
          variant="subtle"
          class="rounded-full"
          :aria-label="`Hire outcome: ${hireOutcomeLabels[hireOutcome]}`"
        >
          {{ hireOutcomeLabels[hireOutcome] }}
        </UBadge>
      </div>
    </div>
  </header>
</template>
