<script setup lang="ts">
import type { CandidateHireOutcome, CandidateReviewStatus } from '@/features/candidates/api'

interface Decision {
  status: Exclude<CandidateReviewStatus, 'new'>
  label: string
  shortcut: string
  color: 'success' | 'warning' | 'error'
}

// Glossary verbs (ADR-0008 #11); the current status renders filled, the rest outline.
const decisions: Decision[] = [
  { status: 'shortlisted', label: 'Shortlist', shortcut: 'S', color: 'success' },
  { status: 'flagged', label: 'Flag', shortcut: 'F', color: 'warning' },
  { status: 'rejected', label: 'Reject', shortcut: 'R', color: 'error' },
]

interface Outcome {
  outcome: Exclude<CandidateHireOutcome, 'none'>
  label: string
  shortcut: string
  color: 'success' | 'error' | 'neutral'
}

const outcomes: Outcome[] = [
  { outcome: 'hired', label: 'Mark Hired', shortcut: 'H', color: 'success' },
  { outcome: 'runaway', label: 'Mark Runaway', shortcut: 'U', color: 'error' },
  { outcome: 'declined', label: 'Mark Declined', shortcut: 'D', color: 'neutral' },
]

const props = withDefaults(
  defineProps<{
    reviewStatus: CandidateReviewStatus
    hireOutcome: CandidateHireOutcome
    canSetOutcome: boolean
    canPrev: boolean
    canNext: boolean
    busy?: boolean
    error?: string | null
    outcomeBusy?: boolean
    outcomeError?: string | null
  }>(),
  { busy: false, error: null, outcomeBusy: false, outcomeError: null },
)

const emit = defineEmits<{
  prev: []
  next: []
  decide: [status: CandidateReviewStatus]
  setOutcome: [outcome: CandidateHireOutcome]
  help: []
}>()

function outcomeDisabled(outcome: Exclude<CandidateHireOutcome, 'none'>): boolean {
  if (!props.canSetOutcome || props.busy || props.outcomeBusy) {
    return true
  }
  if (outcome === 'runaway') {
    return props.hireOutcome !== 'hired'
  }
  if (outcome === 'declined') {
    return props.hireOutcome !== 'none'
  }
  return props.hireOutcome === 'hired'
}
</script>

<template>
  <div
    class="sticky bottom-4 z-10 flex flex-col gap-2 rounded-xl border border-default bg-default px-4 py-3 shadow-lg"
    aria-label="Review actions"
  >
    <p v-if="error" class="m-0 w-full text-sm text-error" role="alert">{{ error }}</p>
    <p v-if="outcomeError" class="m-0 w-full text-sm text-error" role="alert">{{ outcomeError }}</p>
    <div class="flex flex-wrap items-center justify-between gap-3">
      <UButton
        color="neutral"
        variant="outline"
        class="min-h-10"
        :disabled="!canPrev || busy"
        aria-keyshortcuts="ArrowLeft"
        @click="emit('prev')"
      >
        Prev
        <kbd
          class="ml-1 rounded border border-current/40 px-1.5 py-0.5 text-[0.65rem] font-semibold"
          aria-hidden="true"
        >←</kbd>
      </UButton>

      <div class="flex flex-wrap items-center gap-2">
        <UButton
          v-for="decision in decisions"
          :key="decision.status"
          :color="decision.color"
          :variant="reviewStatus === decision.status ? 'solid' : 'outline'"
          class="min-h-10"
          :disabled="busy"
          :aria-keyshortcuts="decision.shortcut"
          @click="emit('decide', decision.status)"
        >
          {{ decision.label }}
          <kbd
            class="ml-1 rounded border border-current/40 px-1.5 py-0.5 text-[0.65rem] font-semibold"
            aria-hidden="true"
          >{{ decision.shortcut }}</kbd>
        </UButton>
      </div>

      <div
        v-if="reviewStatus === 'shortlisted'"
        class="flex flex-wrap items-center gap-2"
        role="group"
        aria-label="Hire outcomes"
      >
        <UButton
          v-for="outcome in outcomes"
          :key="outcome.outcome"
          :color="outcome.color"
          :variant="hireOutcome === outcome.outcome ? 'solid' : 'outline'"
          class="min-h-10"
          :disabled="outcomeDisabled(outcome.outcome)"
          :aria-keyshortcuts="outcome.shortcut"
          @click="emit('setOutcome', outcome.outcome)"
        >
          {{ outcome.label }}
          <kbd
            class="ml-1 rounded border border-current/40 px-1.5 py-0.5 text-[0.65rem] font-semibold"
            aria-hidden="true"
          >{{ outcome.shortcut }}</kbd>
        </UButton>
        <UButton
          v-if="hireOutcome !== 'none'"
          color="neutral"
          variant="ghost"
          class="min-h-10"
          :disabled="!canSetOutcome || busy || outcomeBusy"
          @click="emit('setOutcome', 'none')"
        >
          Clear outcome
        </UButton>
      </div>

      <div class="flex flex-wrap items-center gap-2">
        <UButton
          color="neutral"
          variant="outline"
          class="min-h-10"
          :disabled="!canNext || busy"
          aria-keyshortcuts="ArrowRight"
          @click="emit('next')"
        >
          Next
          <kbd
            class="ml-1 rounded border border-current/40 px-1.5 py-0.5 text-[0.65rem] font-semibold"
            aria-hidden="true"
          >→</kbd>
        </UButton>

        <UButton
          color="neutral"
          variant="outline"
          class="min-h-10"
          icon="i-lucide-keyboard"
          aria-label="Keyboard shortcuts"
          aria-keyshortcuts="?"
          title="Open keyboard shortcuts (?)"
          @click="emit('help')"
        >
          Shortcuts
          <kbd
            class="ml-1 rounded border border-current/40 px-1.5 py-0.5 text-[0.65rem] font-semibold"
            aria-hidden="true"
          >?</kbd>
        </UButton>
      </div>
    </div>
  </div>
</template>
