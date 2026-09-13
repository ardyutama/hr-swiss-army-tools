<script setup lang="ts">
import type { CandidateSummary } from '../api'
import { candidateDisplayName } from '../format'

const open = defineModel<boolean>('open', { required: true })

const props = withDefaults(
  defineProps<{
    candidate: CandidateSummary | null
    deleting?: boolean
    error?: string | null
  }>(),
  { deleting: false, error: null },
)

const emit = defineEmits<{
  confirm: [candidate: CandidateSummary]
}>()

function confirm() {
  if (props.candidate) {
    emit('confirm', props.candidate)
  }
}

</script>

<template>
  <UModal
    v-model:open="open"
    title="Delete candidate"
    :dismissible="!deleting"
  >
    <template #body>
      <p class="confirm__text text-base leading-relaxed text-highlighted">
        Delete <strong>{{ props.candidate ? candidateDisplayName(props.candidate) : '' }}</strong
        >? This permanently removes the candidate and their CV documents and can't be undone.
      </p>
      <UAlert
        v-if="props.error"
        color="error"
        variant="subtle"
        icon="i-lucide-triangle-alert"
        :title="props.error"
        role="alert"
        class="mt-3"
      />
    </template>

    <template #footer>
      <div class="flex justify-end gap-2">
        <UButton color="neutral" variant="outline" :disabled="props.deleting" @click="open = false">
          Cancel
        </UButton>
        <UButton color="error" :loading="props.deleting" @click="confirm">
          Delete candidate
        </UButton>
      </div>
    </template>
  </UModal>
</template>
