<script setup lang="ts">
import { computed } from 'vue'
import { formatDate } from '@/features/vacancies/format'
import { formatReceivedAt } from '@/features/candidates/format'
import type { CandidateHireOutcome, CandidateReviewStatus } from '@/features/candidates/api'
import type { LocationQueryRaw } from 'vue-router'

const intakeSourceLabels: Record<string, string> = {
  email: 'Source Email',
  form: 'Form Response',
}

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

const props = defineProps<{
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
  /** Which evidence the workspace renders: a Source Email or a Form Response. */
  intakeSource: string
  /** The form Timestamp (system data, form variant only); raw or parsed ISO. */
  submittedAt: string | null
  isResubmitted: boolean
  /** The stored CV link for form candidates; null when the cell is empty. */
  cvLink: string | null
  /** The form layout is still loading, so the CV-link state is unknown. */
  cvLinkPending: boolean
}>()

const isFormVariant = computed(() => props.intakeSource === 'form')
const sourceLabel = computed(
  () => intakeSourceLabels[props.intakeSource] ?? props.intakeSource,
)
const submittedAtLabel = computed(() =>
  props.submittedAt ? formatReceivedAt(props.submittedAt) : null,
)
</script>

<template>
  <header class="flex flex-col gap-2">
    <div class="flex flex-wrap items-center gap-3">
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
    </div>

    <!-- Source-identity meta line (issue 04, decision 5): same grammar on both
         variants; the form Timestamp is system data, never a picked column. -->
    <div class="flex flex-wrap items-center gap-x-2 gap-y-1.5 text-sm text-muted">
      <UBadge color="neutral" variant="subtle" class="rounded-full">{{ sourceLabel }}</UBadge>
      <template v-if="submittedAtLabel">
        <span aria-hidden="true">·</span>
        <span>Submitted {{ submittedAtLabel }}</span>
      </template>
      <template v-if="isResubmitted">
        <span aria-hidden="true">·</span>
        <UTooltip
          text="A newer form response replaced the stored one on a later upload. Review status, notes, and typed details were kept."
        >
          <UBadge color="neutral" variant="subtle" class="rounded-full">Resubmitted</UBadge>
        </UTooltip>
      </template>

      <!-- The CV link is a header action on the form variant; the disabled gap
           state never hides, so HR sees the missing link (decision 1). -->
      <span v-if="isFormVariant" class="ml-auto inline-flex items-center">
        <USkeleton v-if="cvLinkPending" class="h-8 w-28 rounded-xl" aria-hidden="true" />
        <UButton
          v-else-if="cvLink"
          :to="cvLink"
          target="_blank"
          rel="noopener"
          icon="i-lucide-external-link"
          color="neutral"
          variant="outline"
          aria-keyshortcuts="C"
          title="Open CV in a new tab (C)"
        >
          Open CV
          <kbd
            class="ml-1 rounded border border-current/40 px-1.5 py-0.5 text-[0.65rem] font-semibold"
            aria-hidden="true"
          >C</kbd>
        </UButton>
        <UButton
          v-else
          disabled
          icon="i-lucide-file-warning"
          color="neutral"
          variant="outline"
          title="The form's CV link cell is empty"
        >
          No CV link
        </UButton>
      </span>
    </div>
  </header>
</template>
