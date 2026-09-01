<script setup lang="ts">
import type { CandidateSummary } from '../api'
import { candidateDisplayName } from '../format'

withDefaults(
  defineProps<{
    open: boolean
    candidate: CandidateSummary | null
    deleting?: boolean
    error?: string | null
  }>(),
  { deleting: false, error: null },
)

const emit = defineEmits<{
  close: []
  confirm: []
}>()
</script>

<template>
  <UModal
    :open="open"
    title="Delete candidate"
    :dismissible="!deleting"
    @update:open="(value) => { if (!value) emit('close') }"
  >
    <template #body>
      <p class="confirm__text text-base leading-relaxed text-highlighted">
        Delete <strong>{{ candidate ? candidateDisplayName(candidate) : '' }}</strong
        >? This permanently removes the candidate and their CV documents and can't be undone.
      </p>
      <UAlert
        v-if="error"
        color="error"
        variant="subtle"
        icon="i-lucide-triangle-alert"
        :title="error"
        role="alert"
        class="mt-3"
      />
    </template>

    <template #footer>
      <div class="flex justify-end gap-2">
        <UButton color="neutral" variant="outline" :disabled="deleting" @click="emit('close')">
          Cancel
        </UButton>
        <UButton color="error" :loading="deleting" @click="emit('confirm')">
          Delete candidate
        </UButton>
      </div>
    </template>
  </UModal>
</template>
