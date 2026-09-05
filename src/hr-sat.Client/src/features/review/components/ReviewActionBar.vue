<script setup lang="ts">
import { onBeforeUnmount, onMounted } from 'vue'
import type { CandidateReviewStatus } from '@/features/candidates/api'

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

const props = withDefaults(
  defineProps<{
    reviewStatus: CandidateReviewStatus
    canPrev: boolean
    canNext: boolean
    busy?: boolean
    error?: string | null
  }>(),
  { busy: false, error: null },
)

const emit = defineEmits<{
  prev: []
  next: []
  decide: [status: CandidateReviewStatus]
}>()

function isEditableTarget(target: EventTarget | null): boolean {
  const element = target as HTMLElement | null
  return (
    !!element &&
    (element.tagName === 'TEXTAREA' ||
      element.tagName === 'INPUT' ||
      element.tagName === 'SELECT' ||
      element.isContentEditable)
  )
}

// Keyboard hints from ADR-0008 #8: ←/→ navigate, S/F/R decide. Editable fields
// keep their keystrokes; ←/→ never drive the PDF viewer.
function onKeydown(event: KeyboardEvent) {
  if (event.defaultPrevented || event.metaKey || event.ctrlKey || event.altKey) {
    return
  }
  if (isEditableTarget(event.target)) {
    return
  }
  if (event.key === 'ArrowLeft' && props.canPrev) {
    event.preventDefault()
    emit('prev')
    return
  }
  if (event.key === 'ArrowRight' && props.canNext) {
    event.preventDefault()
    emit('next')
    return
  }
  const decision = decisions.find(
    (candidate) => candidate.shortcut.toLowerCase() === event.key.toLowerCase(),
  )
  if (decision && !props.busy) {
    event.preventDefault()
    emit('decide', decision.status)
  }
}

onMounted(() => window.addEventListener('keydown', onKeydown))
onBeforeUnmount(() => window.removeEventListener('keydown', onKeydown))
</script>

<template>
  <div
    class="sticky bottom-4 z-10 flex flex-col gap-2 rounded-xl border border-default bg-default px-4 py-3 shadow-lg"
    aria-label="Review actions"
  >
    <p v-if="error" class="m-0 w-full text-sm text-error" role="alert">{{ error }}</p>
    <div class="flex flex-wrap items-center justify-between gap-3">
      <UButton
        color="neutral"
        variant="outline"
        class="min-h-10"
        :disabled="!canPrev || busy"
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
          @click="emit('decide', decision.status)"
        >
          {{ decision.label }}
          <kbd
            class="ml-1 rounded border border-current/40 px-1.5 py-0.5 text-[0.65rem] font-semibold"
            aria-hidden="true"
          >{{ decision.shortcut }}</kbd>
        </UButton>
      </div>

      <UButton
        color="neutral"
        variant="outline"
        class="min-h-10"
        :disabled="!canNext || busy"
        @click="emit('next')"
      >
        Next
        <kbd
          class="ml-1 rounded border border-current/40 px-1.5 py-0.5 text-[0.65rem] font-semibold"
          aria-hidden="true"
        >→</kbd>
      </UButton>
    </div>
  </div>
</template>
