<script setup lang="ts">
import AppButton from '@/shared/ui/AppButton.vue'
import AppDialog from '@/shared/ui/AppDialog.vue'
import type { VacancySummary } from '../api'

withDefaults(
  defineProps<{
    open: boolean
    vacancy: VacancySummary | null
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
  <AppDialog :open="open" title="Delete vacancy" @close="emit('close')">
    <p class="confirm__text">
      Delete <strong>{{ vacancy?.title }}</strong
      >? This permanently removes the vacancy and can't be undone.
    </p>
    <p v-if="error" class="confirm__error" role="alert">{{ error }}</p>

    <template #footer>
      <AppButton variant="ghost" :disabled="deleting" @click="emit('close')">Cancel</AppButton>
      <AppButton variant="danger-solid" :loading="deleting" @click="emit('confirm')">
        Delete vacancy
      </AppButton>
    </template>
  </AppDialog>
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

.confirm__error {
  margin: var(--space-3) 0 0;
  padding: var(--space-3);
  border-radius: var(--radius-sm);
  background: var(--danger-soft);
  color: var(--danger);
  font-size: var(--text-sm);
  font-weight: 500;
}
</style>
