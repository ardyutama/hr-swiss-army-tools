<script setup lang="ts">
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
      <p class="text-base leading-relaxed">
        Close <strong class="font-semibold">{{ props.round ? roundDisplayName(props.round) : '' }}</strong
        >? Closing is permanent — the round can't be reopened and becomes read-only.
      </p>
      <p v-if="props.shortage !== null && props.shortage > 0" class="text-base leading-relaxed">
        {{ props.shortage }} slots still open &mdash; close this round?
      </p>
    </template>

    <template #footer>
      <div class="flex justify-end gap-2">
        <UButton color="neutral" variant="outline" :disabled="props.closing" @click="open = false">
          Cancel
        </UButton>
        <UButton color="error" :loading="props.closing" @click="confirm">
          Close round
        </UButton>
      </div>
    </template>
  </UModal>
</template>
