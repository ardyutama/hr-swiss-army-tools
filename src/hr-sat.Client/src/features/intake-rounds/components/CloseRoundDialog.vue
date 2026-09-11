<script setup lang="ts">
import { computed } from 'vue'
import type { VacancyRound } from '@/features/vacancies/api'
import { roundDisplayName } from '../useIntakeRounds'

const open = defineModel<boolean>('open', { required: true })

const props = withDefaults(
  defineProps<{
    round: VacancyRound | null
    closing?: boolean
    shortage?: number | null
  }>(),
  { closing: false, shortage: null },
)

const emit = defineEmits<{
  confirm: [round: VacancyRound]
}>()

const candidateCountLabel = computed(() => {
  const count = props.round?.candidateCount ?? 0
  return `${count} ${count === 1 ? 'candidate' : 'candidates'}`
})

const shortageLabel = computed(() => {
  const shortage = props.shortage ?? 0
  return `${shortage} ${shortage === 1 ? 'slot' : 'slots'} still open`
})

function confirm() {
  if (props.round) {
    emit('confirm', props.round)
  }
}
</script>

<template>
  <UModal
    v-model:open="open"
    title="Close round"
    :dismissible="!props.closing"
  >
    <template #body>
      <div class="flex flex-col gap-4">
        <!-- What is being closed -->
        <div
          class="flex items-center justify-between gap-3 rounded-xl border border-default bg-muted/30 px-4 py-3"
        >
          <div class="min-w-0">
            <p class="truncate text-sm font-semibold text-highlighted">
              {{ props.round ? roundDisplayName(props.round) : '' }}
            </p>
            <p class="text-sm tabular-nums text-muted">{{ candidateCountLabel }} in this round</p>
          </div>
          <UBadge v-if="props.round" color="success" variant="subtle" class="shrink-0">Open</UBadge>
        </div>

        <!-- What closing means -->
        <p class="text-sm leading-relaxed text-muted">
          Closing is permanent &mdash; the round becomes read-only and can't be reopened.
        </p>

        <!-- Why you might hold off -->
        <UAlert
          v-if="props.shortage !== null && props.shortage > 0"
          color="error"
          variant="subtle"
          icon="i-lucide-circle-alert"
          :title="shortageLabel"
          description="To keep hiring, open a new round after closing this one."
        />
      </div>
    </template>

    <template #footer>
      <div class="flex justify-end gap-2">
        <UButton color="neutral" variant="outline" :disabled="props.closing" @click="open = false">
          Cancel
        </UButton>
        <UButton color="error" icon="i-lucide-lock" :loading="props.closing" @click="confirm">
          Close round
        </UButton>
      </div>
    </template>
  </UModal>
</template>
