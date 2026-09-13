<script setup lang="ts">
import UDropdownMenu from '@nuxt/ui/runtime/components/DropdownMenu.vue'
import type { DropdownMenuItem } from '@nuxt/ui'
import { computed } from 'vue'
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
  icon: string
}

const outcomes: Outcome[] = [
  { outcome: 'hired', label: 'Mark Hired', shortcut: 'H', color: 'success', icon: 'i-lucide-badge-check' },
  { outcome: 'runaway', label: 'Mark Runaway', shortcut: 'U', color: 'error', icon: 'i-lucide-log-out' },
  { outcome: 'declined', label: 'Mark Declined', shortcut: 'D', color: 'neutral', icon: 'i-lucide-circle-x' },
]

const props = withDefaults(
  defineProps<{
    reviewStatus: CandidateReviewStatus
    hireOutcome: CandidateHireOutcome
    canSetOutcome: boolean
    isRoundClosed: boolean
    vacancyClosed: boolean
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

const currentOutcome = computed(() => outcomes.find((outcome) => outcome.outcome === props.hireOutcome) ?? null)
const outcomeTriggerLabel = computed(() => currentOutcome.value?.label.replace('Mark ', '') ?? 'Set outcome')
const outcomeTriggerAriaLabel = computed(() =>
  currentOutcome.value ? `Hire outcome: ${outcomeTriggerLabel.value}` : 'Set hire outcome',
)
// Dashed circle reads as "empty slot" when no outcome is recorded yet.
const outcomeTriggerIcon = computed(() => currentOutcome.value?.icon ?? 'i-lucide-circle-dashed')

const outcomeTriggerClass = computed(() => {
  if (props.hireOutcome === 'hired') {
    return 'border-success/30 bg-success/10 text-success hover:bg-success/15'
  }
  if (props.hireOutcome === 'runaway') {
    return 'border-error/30 bg-error/10 text-error hover:bg-error/15'
  }
  if (props.hireOutcome === 'declined') {
    return 'border-default bg-muted text-highlighted hover:bg-muted/80'
  }
  return 'border-dashed'
})

const outcomeItems = computed<DropdownMenuItem[][]>(() => [
  outcomes.map((outcome) => ({
    label: outcome.label,
    icon: outcome.icon,
    color: outcome.color,
    kbds: [`⇧${outcome.shortcut}`],
    disabled: outcomeDisabled(outcome.outcome),
    onSelect: () => emit('setOutcome', outcome.outcome),
  })),
  [{ type: 'separator' }],
  [{
    label: 'Clear outcome',
    icon: 'i-lucide-eraser',
    disabled: props.hireOutcome === 'none' || !props.canSetOutcome || props.busy || props.outcomeBusy,
    onSelect: () => emit('setOutcome', 'none'),
  }],
])
</script>

<template>
  <div
    class="sticky bottom-4 z-10 flex flex-col gap-2 rounded-xl border border-default bg-default px-4 py-3 shadow-lg"
    aria-label="Review actions"
  >
    <p
      v-if="error"
      class="m-0 flex w-full items-center gap-2 rounded-lg border border-error/30 bg-error/10 px-3 py-2 text-sm text-error"
      role="alert"
    >
      <UIcon name="i-lucide-triangle-alert" class="size-4 shrink-0" aria-hidden="true" />
      {{ error }}
    </p>
    <p
      v-if="outcomeError"
      class="m-0 flex w-full items-center gap-2 rounded-lg border border-error/30 bg-error/10 px-3 py-2 text-sm text-error"
      role="alert"
    >
      <UIcon name="i-lucide-triangle-alert" class="size-4 shrink-0" aria-hidden="true" />
      {{ outcomeError }}
    </p>
    <div class="flex flex-wrap items-center justify-between gap-x-4 gap-y-2">
      <UButton
        color="neutral"
        variant="outline"
        class="min-h-10"
        icon="i-lucide-arrow-left"
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

      <div class="flex flex-wrap items-center gap-3">
        <div
          v-if="!isRoundClosed && !vacancyClosed"
          class="flex items-center gap-1 rounded-xl bg-muted/40 p-1"
          role="group"
          aria-label="Review decision"
        >
          <UButton
            v-for="decision in decisions"
            :key="decision.status"
            :color="decision.color"
            :variant="reviewStatus === decision.status ? 'solid' : 'outline'"
            class="min-h-10"
            :disabled="busy"
            :aria-pressed="reviewStatus === decision.status"
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
          v-if="reviewStatus === 'shortlisted' && !vacancyClosed"
          class="flex flex-wrap items-center gap-2"
          role="group"
          aria-label="Hire outcomes"
        >
          <UDropdownMenu :items="outcomeItems" :ui="{ item: 'min-h-10' }">
            <UButton
              color="neutral"
              variant="outline"
              class="min-h-10 min-w-48 justify-between"
              :class="outcomeTriggerClass"
              :icon="outcomeTriggerIcon"
              trailing-icon="i-lucide-chevron-down"
              :loading="outcomeBusy"
              :disabled="!canSetOutcome || busy"
              :aria-label="outcomeTriggerAriaLabel"
              title="Hire outcome options"
            >
              <span v-if="currentOutcome" class="flex items-baseline gap-1.5">
                <span class="text-xs font-normal opacity-70">Outcome</span>
                <span class="font-semibold">{{ outcomeTriggerLabel }}</span>
              </span>
              <span v-else>{{ outcomeTriggerLabel }}</span>
            </UButton>
          </UDropdownMenu>
        </div>
      </div>

      <div class="flex flex-wrap items-center gap-2">
        <UButton
          color="primary"
          variant="solid"
          class="min-h-10"
          trailing-icon="i-lucide-arrow-right"
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
