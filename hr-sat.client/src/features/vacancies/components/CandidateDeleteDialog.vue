<script setup lang="ts">
import ConfirmDialog from '@/shared/ui/ConfirmDialog.vue'
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
  <ConfirmDialog
    :open="open"
    title="Delete candidate"
    confirm-label="Delete candidate"
    :confirming="deleting"
    :error="error"
    @close="emit('close')"
    @confirm="emit('confirm')"
  >
    <p class="confirm__text">
      Delete <strong>{{ candidate ? candidateDisplayName(candidate) : '' }}</strong
      >? This permanently removes the candidate and their CV documents and can't be undone.
    </p>
  </ConfirmDialog>
</template>

<style scoped>
.confirm__text {
  margin: 0;
  font-size: var(--text-base);
  color: var(--text);
  line-height: 1.6;
}

.confirm__text strong {
  font-weight: 600;
}
</style>
